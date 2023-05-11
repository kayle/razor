// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.ProjectContext;

[LanguageServerEndpoint(VSMethods.GetProjectContextsName)]
internal sealed class ProjectContextEndpoint : IRegistrationExtension, IRazorRequestHandler<VSGetProjectContextsParams, IEnumerable<VSProjectContext>?>
{
    public bool MutatesSolutionState => throw new NotImplementedException();

    public RegistrationExtensionResult? GetRegistration(VSInternalClientCapabilities clientCapabilities)
        => new("_vs_projectContextProvider", options: true);

    public TextDocumentIdentifier GetTextDocumentIdentifier(VSGetProjectContextsParams request)
    {
        if (request.TextDocument is null)
        {
            throw new ArgumentNullException(nameof(request.TextDocument));
        }

        return new TextDocumentIdentifier()
        {
            Uri = request.TextDocument.Uri
        };
    }

    public async Task<IEnumerable<VSProjectContext>?> HandleRequestAsync(VSGetProjectContextsParams request, RazorRequestContext requestContext, CancellationToken cancellationToken)
    {
        var documentContext = requestContext.GetRequiredDocumentContext();
        return new[]
        {
            new VSProjectContext()
            {
                Label = "Test",
                Id = "Test",
                Kind = VSProjectKind.CSharp
            }
        };
    }
}
