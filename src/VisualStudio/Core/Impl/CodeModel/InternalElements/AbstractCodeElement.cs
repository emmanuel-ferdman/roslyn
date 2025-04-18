﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.VisualStudio.LanguageServices.Implementation.Interop;
using Microsoft.VisualStudio.LanguageServices.Implementation.Utilities;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.CodeModel.InternalElements;

/// <summary>
/// This is the base class of all code elements.
/// </summary>
public abstract class AbstractCodeElement : AbstractCodeModelObject, ICodeElementContainer<AbstractCodeElement>, EnvDTE.CodeElement, EnvDTE80.CodeElement2
{
    private readonly ComHandle<EnvDTE.FileCodeModel, FileCodeModel> _fileCodeModel;
    private readonly int? _nodeKind;

    internal AbstractCodeElement(
        CodeModelState state,
        FileCodeModel fileCodeModel,
        int? nodeKind = null)
        : base(state)
    {
        Debug.Assert(fileCodeModel != null);

        _fileCodeModel = new ComHandle<EnvDTE.FileCodeModel, FileCodeModel>(fileCodeModel);
        _nodeKind = nodeKind;
    }

    internal FileCodeModel FileCodeModel
        => _fileCodeModel.Object;

    protected SyntaxTree GetSyntaxTree()
        => FileCodeModel.GetSyntaxTree();

    protected Document GetDocument()
        => FileCodeModel.GetDocument();

    protected SemanticModel GetSemanticModel()
        => FileCodeModel.GetSemanticModel();

    protected ProjectId GetProjectId()
        => FileCodeModel.GetProjectId();

    internal bool IsValidNode()
    {
        if (!TryLookupNode(out var node))
        {
            return false;
        }

        if (_nodeKind != null &&
            _nodeKind.Value != node.RawKind)
        {
            return false;
        }

        return true;
    }

    internal virtual SyntaxNode LookupNode()
    {
        if (!TryLookupNode(out var node))
        {
            throw Exceptions.ThrowEFail();
        }

        return node;
    }

    internal abstract bool TryLookupNode(out SyntaxNode node);

    internal virtual ISymbol LookupSymbol()
    {
        var semanticModel = GetSemanticModel();
        var node = LookupNode();
        return semanticModel.GetDeclaredSymbol(node);
    }

    protected void UpdateNode<T>(Action<SyntaxNode, T> updater, T value)
    {
        FileCodeModel.EnsureEditor(() =>
        {
            var node = LookupNode();
            updater(node, value);
        });
    }

    public abstract EnvDTE.vsCMElement Kind { get; }

    protected virtual string GetName()
    {
        var node = LookupNode();
        return CodeModelService.GetName(node);
    }

    protected virtual void SetName(string value)
        => UpdateNode(FileCodeModel.UpdateName, value);

    public string Name
    {
        get { return GetName(); }
        set { SetName(value); }
    }

    protected virtual string GetFullName()
    {
        var node = LookupNode();
        var semanticModel = GetSemanticModel();
        return CodeModelService.GetFullName(node, semanticModel);
    }

    public string FullName
    {
        get { return GetFullName(); }
    }

    public abstract object Parent { get; }

    public abstract EnvDTE.CodeElements Children { get; }

    EnvDTE.CodeElements ICodeElementContainer<AbstractCodeElement>.GetCollection()
        => Children;

    protected virtual EnvDTE.CodeElements GetCollection()
        => GetCollection<AbstractCodeElement>(Parent);

    public virtual EnvDTE.CodeElements Collection
    {
        get { return GetCollection(); }
    }

    private LineFormattingOptions GetLineFormattingOptions()
        => State.ThreadingContext.JoinableTaskFactory.Run(() => GetDocument().GetLineFormattingOptionsAsync(CancellationToken.None).AsTask());

    public EnvDTE.TextPoint StartPoint
    {
        get
        {
            var options = GetLineFormattingOptions();
            var point = CodeModelService.GetStartPoint(LookupNode(), options);
            if (point == null)
            {
                return null;
            }

            return FileCodeModel.TextManagerAdapter.CreateTextPoint(FileCodeModel, point.Value);
        }
    }

    public EnvDTE.TextPoint EndPoint
    {
        get
        {
            var options = GetLineFormattingOptions();
            var point = CodeModelService.GetEndPoint(LookupNode(), options);
            if (point == null)
            {
                return null;
            }

            return FileCodeModel.TextManagerAdapter.CreateTextPoint(FileCodeModel, point.Value);
        }
    }

    public virtual EnvDTE.TextPoint GetStartPoint(EnvDTE.vsCMPart part)
    {
        var options = GetLineFormattingOptions();
        var point = CodeModelService.GetStartPoint(LookupNode(), options, part);
        if (point == null)
        {
            return null;
        }

        return FileCodeModel.TextManagerAdapter.CreateTextPoint(FileCodeModel, point.Value);
    }

    public virtual EnvDTE.TextPoint GetEndPoint(EnvDTE.vsCMPart part)
    {
        var options = GetLineFormattingOptions();
        var point = CodeModelService.GetEndPoint(LookupNode(), options, part);
        if (point == null)
        {
            return null;
        }

        return FileCodeModel.TextManagerAdapter.CreateTextPoint(FileCodeModel, point.Value);
    }

    public virtual EnvDTE.vsCMInfoLocation InfoLocation
    {
        get
        {
            // The default implementation assumes project-located elements...
            return EnvDTE.vsCMInfoLocation.vsCMInfoLocationProject;
        }
    }

    public virtual bool IsCodeType
    {
        get { return false; }
    }

    public EnvDTE.ProjectItem ProjectItem
    {
        get { return FileCodeModel.Parent; }
    }

    public string ExtenderCATID
    {
        get { throw new NotImplementedException(); }
    }

    protected virtual object GetExtenderNames()
        => throw Exceptions.ThrowENotImpl();

    public object ExtenderNames
    {
        get { return GetExtenderNames(); }
    }

    protected virtual object GetExtender(string name)
        => throw Exceptions.ThrowENotImpl();

    public object get_Extender(string extenderName)
        => GetExtender(extenderName);

    public string ElementID
    {
        get { throw new NotImplementedException(); }
    }

    public virtual void RenameSymbol(string newName)
    {
        if (string.IsNullOrEmpty(newName))
        {
            throw new ArgumentException();
        }

        CodeModelService.Rename(LookupSymbol(), newName, this.Workspace, this.State.ProjectCodeModelFactory);
    }

    protected virtual Document DeleteCore(Document document)
    {
        var node = LookupNode();
        return CodeModelService.Delete(document, node);
    }

    /// <summary>
    /// Delete the element from the source file.
    /// </summary>
    internal void Delete()
    {
        FileCodeModel.PerformEdit(document =>
        {
            return DeleteCore(document);
        });
    }

    [SuppressMessage("Microsoft.StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Required by interface")]
    public string get_Prototype(int flags)
        => CodeModelService.GetPrototype(LookupNode(), LookupSymbol(), (PrototypeFlags)flags);
}
