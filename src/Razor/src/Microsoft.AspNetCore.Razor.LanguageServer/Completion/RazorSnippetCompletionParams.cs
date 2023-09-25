﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion;

internal record RazorSnippetCompletionParams(RazorLanguageKind LanguageKind, string Text);
