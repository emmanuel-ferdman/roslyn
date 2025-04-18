﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.LanguageServer.Handler.Commands;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Roslyn.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.LanguageServer.Handler;

[ExportCSharpVisualBasicStatelessLspService(typeof(ExecuteWorkspaceCommandHandler)), Shared]
[Method(Methods.WorkspaceExecuteCommandName)]
internal sealed class ExecuteWorkspaceCommandHandler : ILspServiceRequestHandler<ExecuteCommandParams, object?>
{
    public bool MutatesSolutionState => false;

    public bool RequiresLSPSolution => true;

    [ImportingConstructor]
    [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
    public ExecuteWorkspaceCommandHandler()
    {
    }

    public async Task<object?> HandleRequestAsync(ExecuteCommandParams request, RequestContext context, CancellationToken cancellationToken)
    {
        var handlerProvider = context.GetRequiredService<AbstractHandlerProvider>();
        var lspServices = context.GetRequiredService<ILspServices>();

        var requestMethod = AbstractExecuteWorkspaceCommandHandler.GetRequestNameForCommandName(request.Command);

        var handler = (AbstractExecuteWorkspaceCommandHandler)handlerProvider.GetMethodHandler(requestMethod, TypeRef.FromOrNull(typeof(ExecuteCommandParams)), TypeRef.FromOrNull(typeof(object)), LanguageServerConstants.DefaultLanguageName);

        var result = await handler.HandleRequestAsync(request, context, cancellationToken).ConfigureAwait(false);

        return result;
    }
}
