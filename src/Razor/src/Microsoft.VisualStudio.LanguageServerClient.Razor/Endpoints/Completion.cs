﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Completion;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Editor.Razor.Snippets;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Extensions;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;
using static Microsoft.VisualStudio.Editor.Razor.Snippets.XmlSnippetParser;
using RazorTextSpan = Microsoft.AspNetCore.Razor.Language.Syntax.TextSpan;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor;

internal partial class RazorCustomMessageTarget
{
    private const string SnippetData = "razor/snippet";

    // Called by the Razor Language Server to provide inline completions from the platform.
    [JsonRpcMethod(CustomMessageNames.RazorInlineCompletionEndpoint, UseSingleObjectParameterDeserialization = true)]
    public async Task<VSInternalInlineCompletionList?> ProvideInlineCompletionAsync(RazorInlineCompletionRequest inlineCompletionParams, CancellationToken cancellationToken)
    {
        if (inlineCompletionParams is null)
        {
            throw new ArgumentNullException(nameof(inlineCompletionParams));
        }

        var hostDocumentUri = inlineCompletionParams.TextDocument.Uri;
        if (!_documentManager.TryGetDocument(hostDocumentUri, out var documentSnapshot))
        {
            return null;
        }

        // TODO: Support multiple C# documents per Razor document.
        if (!documentSnapshot.TryGetVirtualDocument<CSharpVirtualDocumentSnapshot>(out var csharpDoc))
        {
            return null;
        }

        var csharpRequest = new VSInternalInlineCompletionRequest
        {
            Context = inlineCompletionParams.Context,
            Position = inlineCompletionParams.Position,
            TextDocument = inlineCompletionParams.TextDocument.WithUri(csharpDoc.Uri),
            Options = inlineCompletionParams.Options,
        };

        var textBuffer = csharpDoc.Snapshot.TextBuffer;
        var request = await _requestInvoker.ReinvokeRequestOnServerAsync<VSInternalInlineCompletionRequest, VSInternalInlineCompletionList?>(
            textBuffer,
            VSInternalMethods.TextDocumentInlineCompletionName,
            RazorLSPConstants.RazorCSharpLanguageServerName,
            csharpRequest,
            cancellationToken).ConfigureAwait(false);

        return request?.Response;
    }

    [JsonRpcMethod(LanguageServerConstants.RazorCompletionEndpointName, UseSingleObjectParameterDeserialization = true)]
    public async Task<CompletionItem[]?> ProvideCompletionsAsync(
        DelegatedCompletionParams request,
        CancellationToken cancellationToken)
    {
        var hostDocumentUri = request.Identifier.TextDocumentIdentifier.Uri;

        string languageServerName;
        Uri projectedUri;
        bool synchronized;
        VirtualDocumentSnapshot virtualDocumentSnapshot;
        if (request.ProjectedKind == RazorLanguageKind.Html)
        {
            (synchronized, virtualDocumentSnapshot) = await TrySynchronizeVirtualDocumentAsync<HtmlVirtualDocumentSnapshot>(
                request.Identifier.Version,
                request.Identifier.TextDocumentIdentifier,
                cancellationToken);
            languageServerName = RazorLSPConstants.HtmlLanguageServerName;
            projectedUri = virtualDocumentSnapshot.Uri;
        }
        else if (request.ProjectedKind == RazorLanguageKind.CSharp)
        {
            (synchronized, virtualDocumentSnapshot) = await TrySynchronizeVirtualDocumentAsync<CSharpVirtualDocumentSnapshot>(
                request.Identifier.Version,
                request.Identifier.TextDocumentIdentifier,
                cancellationToken);
            languageServerName = RazorLSPConstants.RazorCSharpLanguageServerName;
            projectedUri = virtualDocumentSnapshot.Uri;
        }
        else
        {
            Debug.Fail("Unexpected RazorLanguageKind. This shouldn't really happen in a real scenario.");
            return null;
        }

        if (!synchronized)
        {
            return null;
        }

        var completionParams = new CompletionParams()
        {
            Context = request.Context,
            Position = request.ProjectedPosition,
            TextDocument = request.Identifier.TextDocumentIdentifier.WithUri(projectedUri),
        };

        var continueOnCapturedContext = false;
        var provisionalTextEdit = request.ProvisionalTextEdit;
        if (provisionalTextEdit is not null)
        {
            await _joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var provisionalChange = new VisualStudioTextChange(provisionalTextEdit, virtualDocumentSnapshot.Snapshot);
            UpdateVirtualDocument(provisionalChange, request.ProjectedKind, request.Identifier.Version, hostDocumentUri, virtualDocumentSnapshot.Uri);

            // We want the delegation to continue on the captured context because we're currently on the `main` thread and we need to get back to the
            // main thread in order to update the virtual buffer with the reverted text edit.
            continueOnCapturedContext = true;
        }

        try
        {
            using var _1 = ArrayBuilderPool<CompletionItem>.GetPooledObject(out var builder);

            var textBuffer = virtualDocumentSnapshot.Snapshot.TextBuffer;
            var lspMethodName = Methods.TextDocumentCompletion.Name;
            using var _ = _telemetryReporter.TrackLspRequest(lspMethodName, languageServerName, request.CorrelationId);
            var response = await _requestInvoker.ReinvokeRequestOnServerAsync<CompletionParams, CompletionItem[]?>(
                textBuffer,
                lspMethodName,
                languageServerName,
                completionParams,
                cancellationToken).ConfigureAwait(continueOnCapturedContext);

            if (response?.Response is CompletionItem[] responseItems)
            {
                builder.AddRange(responseItems);
            }

            AddSnippetCompletions(request.ProjectedKind, builder);
            return builder.ToArray();
        }
        finally
        {
            if (provisionalTextEdit is not null)
            {
                var revertedProvisionalTextEdit = BuildRevertedEdit(provisionalTextEdit);
                var revertedProvisionalChange = new VisualStudioTextChange(revertedProvisionalTextEdit, virtualDocumentSnapshot.Snapshot);
                UpdateVirtualDocument(revertedProvisionalChange, request.ProjectedKind, request.Identifier.Version, hostDocumentUri, virtualDocumentSnapshot.Uri);
            }
        }
    }

    private static TextEdit BuildRevertedEdit(TextEdit provisionalTextEdit)
    {
        TextEdit? revertedProvisionalTextEdit;
        if (provisionalTextEdit.Range.Start == provisionalTextEdit.Range.End)
        {
            // Insertion
            revertedProvisionalTextEdit = new TextEdit()
            {
                Range = new Range()
                {
                    Start = provisionalTextEdit.Range.Start,

                    // We're making an assumption that provisional text edits do not span more than 1 line.
                    End = new Position(provisionalTextEdit.Range.End.Line, provisionalTextEdit.Range.End.Character + provisionalTextEdit.NewText.Length),
                },
                NewText = string.Empty
            };
        }
        else
        {
            // Replace
            revertedProvisionalTextEdit = new TextEdit()
            {
                Range = provisionalTextEdit.Range,
                NewText = string.Empty
            };
        }

        return revertedProvisionalTextEdit;
    }

    private void UpdateVirtualDocument(
        VisualStudioTextChange textChange,
        RazorLanguageKind virtualDocumentKind,
        int hostDocumentVersion,
        Uri documentSnapshotUri,
        Uri virtualDocumentUri)
    {
        if (_documentManager is not TrackingLSPDocumentManager trackingDocumentManager)
        {
            return;
        }

        if (virtualDocumentKind == RazorLanguageKind.CSharp)
        {
            trackingDocumentManager.UpdateVirtualDocument<CSharpVirtualDocument>(
                documentSnapshotUri,
                virtualDocumentUri,
                new[] { textChange },
                hostDocumentVersion,
                state: null);
        }
        else if (virtualDocumentKind == RazorLanguageKind.Html)
        {
            trackingDocumentManager.UpdateVirtualDocument<HtmlVirtualDocument>(
                documentSnapshotUri,
                virtualDocumentUri,
                new[] { textChange },
                hostDocumentVersion,
                state: null);
        }
    }

    [JsonRpcMethod(LanguageServerConstants.RazorCompletionResolveEndpointName, UseSingleObjectParameterDeserialization = true)]
    public async Task<CompletionItem?> ProvideResolvedCompletionItemAsync(DelegatedCompletionItemResolveParams request, CancellationToken cancellationToken)
    {
        // Check if we're completing a snippet item that we provided
        if (request.CompletionItem.Data is SnippetCompletionData snippetCompletionData)
        {
            return ResolveSnippetCompletion(request.CompletionItem, snippetCompletionData);
        }

        string languageServerName;
        bool synchronized;
        VirtualDocumentSnapshot virtualDocumentSnapshot;
        if (request.OriginatingKind == RazorLanguageKind.Html)
        {
            (synchronized, virtualDocumentSnapshot) = await TrySynchronizeVirtualDocumentAsync<HtmlVirtualDocumentSnapshot>(
                request.Identifier.Version,
                request.Identifier.TextDocumentIdentifier,
                cancellationToken);
            languageServerName = RazorLSPConstants.HtmlLanguageServerName;
        }
        else if (request.OriginatingKind == RazorLanguageKind.CSharp)
        {
            // TODO this is a partial workaround to fix prefix completion by avoiding sync (which times out during resolve endpoint) if we are currently at a higher version value
            // this does not fix postfix completion and should be superseded by eventual synchronization fix

            var futureDataSyncResult = TryReturnPossiblyFutureSnapshot<CSharpVirtualDocumentSnapshot>(request.Identifier.Version, request.Identifier.TextDocumentIdentifier);
            if (futureDataSyncResult?.Synchronized == true)
            {
                (synchronized, virtualDocumentSnapshot) = futureDataSyncResult;
            }
            else
            {
                (synchronized, virtualDocumentSnapshot) = await TrySynchronizeVirtualDocumentAsync<CSharpVirtualDocumentSnapshot>(
                    request.Identifier.Version,
                    request.Identifier.TextDocumentIdentifier,
                    cancellationToken);
            }

            languageServerName = RazorLSPConstants.RazorCSharpLanguageServerName;
        }
        else
        {
            Debug.Fail("Unexpected RazorLanguageKind. This can't really happen in a real scenario.");
            return null;
        }

        if (!synchronized)
        {
            // Document was not synchronized
            return null;
        }

        var completionResolveParams = request.CompletionItem;

        var textBuffer = virtualDocumentSnapshot.Snapshot.TextBuffer;
        var response = await _requestInvoker.ReinvokeRequestOnServerAsync<VSInternalCompletionItem, CompletionItem?>(
            textBuffer,
            Methods.TextDocumentCompletionResolve.Name,
            languageServerName,
            completionResolveParams,
            cancellationToken).ConfigureAwait(false);
        return response?.Response;
    }

    private CompletionItem? ResolveSnippetCompletion(VSInternalCompletionItem completionItem, SnippetCompletionData snippetCompletionData)
    {
        var snippets = _snippetCache.GetSnippets();
        var snippet = snippets.FirstOrDefault(s => snippetCompletionData.Matches(s));
        if (snippet is null)
        {
            return null;
        }

        var completionString = TryGetReplacedSnippetText(snippet);
        if (completionString is null)
        {
            return null;
        }

        completionItem.InsertText = completionString;
        return completionItem;
    }

    [JsonRpcMethod(LanguageServerConstants.RazorGetFormattingOptionsEndpointName, UseSingleObjectParameterDeserialization = true)]
    public Task<FormattingOptions?> GetFormattingOptionsAsync(TextDocumentIdentifierAndVersion document, CancellationToken _)
    {
        var formattingOptions = _formattingOptionsProvider.GetOptions(document.TextDocumentIdentifier.Uri);
        return Task.FromResult(formattingOptions);
    }

    private void AddSnippetCompletions(RazorLanguageKind languageKind, ImmutableArray<CompletionItem>.Builder builder)
    {
        var snippets = _snippetCache.GetSnippets();
        if (snippets.IsDefaultOrEmpty)
        {
            return;
        }

        var languageSpecificCompletions = languageKind switch
        {
            RazorLanguageKind.Html => snippets.Where(static s => s.Language == SnippetLanguage.Html),
            RazorLanguageKind.CSharp => snippets.Where(static s => s.Language == SnippetLanguage.CSharp),
            _ => null
        };

        builder.AddRange(languageSpecificCompletions
            .Select(s => new CompletionItem()
            {
                Label = s.Shortcut,
                Detail = s.Description,
                InsertTextFormat = InsertTextFormat.Snippet,
                InsertText = s.Shortcut,
                Data = s.CompletionData
            }));
    }

    /// <summary>
    /// Create a snippet that adheres to the https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/#snippet_syntax
    /// </summary>
    private string? TryGetReplacedSnippetText(SnippetInfo snippetInfo)
    {
        var parsedSnippet = _xmlSnippetParser.GetParsedXmlSnippet(snippetInfo);
        if (parsedSnippet is null)
        {
            return null;
        }

        using var _ = StringBuilderPool.GetPooledObject(out var functionSnippetBuilder);

        foreach (var part in parsedSnippet.Parts)
        {
            if (part is SnippetShortcutPart shortcutPart)
            {
                shortcutPart.Shortcut = snippetInfo.Shortcut;
            }

            functionSnippetBuilder.Append(part.ToString());
        }

        return functionSnippetBuilder.ToString();
    }
}
