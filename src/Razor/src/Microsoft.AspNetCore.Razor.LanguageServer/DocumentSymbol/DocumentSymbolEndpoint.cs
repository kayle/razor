// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using DocSymbol = Microsoft.VisualStudio.LanguageServer.Protocol.DocumentSymbol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.DocumentSymbol;

[LanguageServerEndpoint(Methods.TextDocumentDocumentSymbolName)]
internal class DocumentSymbolEndpoint : IRegistrationExtension, IRazorRequestHandler<DocumentSymbolParams, IEnumerable<SymbolInformation>?>
{
    private readonly LanguageServerFeatureOptions _languageServerFeatureOptions;
    private readonly RazorDocumentMappingService _documentMappingService;
    private readonly ClientNotifierServiceBase _languageServer;

    public DocumentSymbolEndpoint(
        LanguageServerFeatureOptions languageServerFeatureOptions,
        RazorDocumentMappingService documentMappingService,
        ClientNotifierServiceBase languageServer)
    {
        _languageServerFeatureOptions = languageServerFeatureOptions ?? throw new ArgumentNullException(nameof(languageServerFeatureOptions));
        _documentMappingService = documentMappingService ?? throw new ArgumentNullException(nameof(documentMappingService));
        _languageServer = languageServer ?? throw new ArgumentNullException(nameof(languageServer));
    }

    public bool MutatesSolutionState => false;

    public RegistrationExtensionResult? GetRegistration(VSInternalClientCapabilities clientCapabilities)
    {
        if (!_languageServerFeatureOptions.SingleServerSupport)
        {
            return null;
        }

        var options = new DocumentSymbolOptions()
        {
            WorkDoneProgress = false
        };

        var result = new SumType<bool, DocumentSymbolOptions>(options);
        return new(serverCapability: "documentSymbolProvider", result);
    }

    public TextDocumentIdentifier GetTextDocumentIdentifier(DocumentSymbolParams request)
    {
        return new TextDocumentIdentifier()
        {
            Uri = request.TextDocument.Uri
        };
    }

    public async Task<IEnumerable<SymbolInformation>?> HandleRequestAsync(DocumentSymbolParams request, RazorRequestContext requestContext, CancellationToken cancellationToken)
    {
        var documentContext = requestContext.GetRequiredDocumentContext();
        var delegatedParams = new DelegatedDocumentParams(documentContext.Identifier);

        var symbols = await _languageServer.SendRequestAsync<DelegatedDocumentParams, SymbolInformation[]?>(
            RazorLanguageServerCustomMessageTargets.RazorDocumentSymbolEndpointName,
            delegatedParams,
            cancellationToken);

        if (symbols is null)
        {
            return null;
        }

        var mappedSymbols = new List<SymbolInformation>();

        foreach (var symbol in symbols)
        {
            var (uri, mappedRange) = await _documentMappingService.MapFromProjectedDocumentRangeAsync(
                symbol.Location.Uri,
                symbol.Location.Range,
                cancellationToken);

            if (mappedRange is null || uri is null)
            {
                continue;
            }

            var mappedSymbol = new SymbolInformation()
            {
                Name = symbol.Name,
                Location = new Location()
                {
                    Uri = uri,
                    Range = mappedRange
                },
                ContainerName = symbol.ContainerName,
                Kind = symbol.Kind,
            };

            mappedSymbols.Add(mappedSymbol);
        }

        return mappedSymbols.ToArray();
    }
}
