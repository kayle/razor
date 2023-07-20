﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test.Common;

internal class TestDocumentContextFactory : DocumentContextFactory
{
    private readonly string? _filePath;
    private readonly RazorCodeDocument? _codeDocument;
    private readonly int? _version;

    public TestDocumentContextFactory()
    {
    }

    public TestDocumentContextFactory(string filePath, RazorCodeDocument codeDocument, int? version = null)
    {
        _filePath = filePath;
        _codeDocument = codeDocument;
        _version = version;
    }

    protected override Task<DocumentContext?> TryCreateAsync(Uri documentUri, VSProjectContext? projectContext, bool versioned, CancellationToken cancellationToken)
    {
        if (_filePath is null || _codeDocument is null)
        {
            return Task.FromResult<DocumentContext?>(null);
        }

        if (versioned)
        {
            if (_version is null)
            {
                return Task.FromResult<DocumentContext?>(null);
            }

            return Task.FromResult<DocumentContext?>(TestDocumentContext.From(_filePath, _codeDocument, _version.Value));
        }

        return Task.FromResult<DocumentContext?>(TestDocumentContext.From(_filePath, _codeDocument));
    }
}
