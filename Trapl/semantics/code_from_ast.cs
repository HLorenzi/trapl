﻿using System.Collections.Generic;
using Trapl.Diagnostics;
using Trapl.Infrastructure;


namespace Trapl.Semantics
{
    public class CodeASTConverter
    {
        public static CodeBody Convert(Infrastructure.Session session, Grammar.ASTNode astNode, List<Variable> localVariables, Type returnType)
        {
            var analyzer = new CodeASTConverter(session, localVariables, returnType);
            var body = new CodeBody();
            body.code = analyzer.ParseBlock(astNode);
            body.localVariables = localVariables;
            body.returnType = returnType;
            return body;
        }


        private Infrastructure.Session session;
        private List<Variable> localVariables;
        private List<bool> localVariablesInScope;
        private Type returnType;


        private CodeASTConverter(Infrastructure.Session session, List<Variable> localVariables, Type returnType)
        {
            this.session = session;
            this.localVariables = localVariables;
            this.localVariablesInScope = new List<bool>();
            foreach (var local in this.localVariables)
                this.localVariablesInScope.Add(true);
            this.returnType = returnType;
        }


        private CodeNode ParseBlock(Grammar.ASTNode astNode)
        {
            var code = new CodeNodeSequence();
            code.span = astNode.Span();
            code.outputType = new TypeTuple();

            var curLocalVariableIndex = this.localVariables.Count;

            foreach (var exprNode in astNode.EnumerateChildren())
            {
                try
                {
                    code.children.Add(this.ParseExpression(exprNode));
                }
                catch (CheckException) { }
            }

            for (int i = curLocalVariableIndex; i < this.localVariables.Count; i++)
                this.localVariablesInScope[i] = false;

            return code;
        }


        private CodeNode ParseExpression(Grammar.ASTNode astNode)
        {
            if (astNode.kind == Grammar.ASTNodeKind.Block)
                return this.ParseBlock(astNode);
            else if (astNode.kind == Grammar.ASTNodeKind.ControlLet)
                return this.ParseControlLet(astNode);
            else if (astNode.kind == Grammar.ASTNodeKind.ControlIf)
                return this.ParseControlIf(astNode);
            else if (astNode.kind == Grammar.ASTNodeKind.ControlWhile)
                return this.ParseControlWhile(astNode);
            else if (astNode.kind == Grammar.ASTNodeKind.ControlReturn)
                return this.ParseControlReturn(astNode);
            else if (astNode.kind == Grammar.ASTNodeKind.Call)
                return this.ParseCall(astNode);
            else if (astNode.kind == Grammar.ASTNodeKind.BinaryOp)
                return this.ParseBinaryOp(astNode);
            else if (astNode.kind == Grammar.ASTNodeKind.UnaryOp)
                return this.ParseUnaryOp(astNode);
            else if (astNode.kind == Grammar.ASTNodeKind.BooleanLiteral)
                return this.ParseBooleanLiteral(astNode);
            else if (astNode.kind == Grammar.ASTNodeKind.NumberLiteral)
                return this.ParseNumberLiteral(astNode);
            else if (astNode.kind == Grammar.ASTNodeKind.StructLiteral)
                return this.ParseStructLiteral(astNode);
            else if (astNode.kind == Grammar.ASTNodeKind.Name)
                return this.ParseName(astNode);
            else
                throw new InternalException("unimplemented");
        }


        private CodeNode ParseControlLet(Grammar.ASTNode astNode)
        {
            var code = new CodeNodeControlLet();
            code.span = astNode.Span();
            code.outputType = new TypeTuple();

            var local = new Variable();
            local.declSpan = astNode.Child(0).Span();

            local.name = new Name(
                astNode.Child(0).Span(),
                astNode.Child(0).Child(0),
                TemplateUtil.ResolveFromNameAST(this.session, astNode.Child(0), true));

            var curChild = 1;

            // Parse type annotation, if there is one.
            local.type = new TypePlaceholder();
            if (astNode.ChildNumber() > curChild &&
                TypeUtil.IsTypeNode(astNode.Child(curChild).kind))
            {
                local.type = TypeUtil.ResolveFromAST(this.session, astNode.Child(curChild), false);
                curChild++;
            }

            code.localIndex = this.localVariables.Count;
            this.localVariables.Add(local);
            this.localVariablesInScope.Add(true);

            // Parse initializer expression, if there is one.
            if (astNode.ChildNumber() > curChild)
            {
                code.children.Add(this.ParseExpression(astNode.Child(curChild)));
                curChild++;
            }

            return code;
        }


        private CodeNode ParseControlIf(Grammar.ASTNode astNode)
        {
            var code = new CodeNodeControlIf();
            code.span = astNode.Span();
            code.outputType = new TypePlaceholder();

            foreach (var child in astNode.children)
                code.children.Add(this.ParseExpression(child));

            return code;
        }


        private CodeNode ParseControlWhile(Grammar.ASTNode astNode)
        {
            var code = new CodeNodeControlWhile();
            code.span = astNode.Span();
            code.outputType = new TypeTuple();

            foreach (var child in astNode.children)
                code.children.Add(this.ParseExpression(child));

            return code;
        }


        private CodeNode ParseControlReturn(Grammar.ASTNode astNode)
        {
            var code = new CodeNodeControlReturn();
            code.span = astNode.Span();
            code.outputType = new TypeTuple();
            code.children.Add(this.ParseExpression(astNode.children[0]));
            return code;
        }


        private CodeNode ParseCall(Grammar.ASTNode astNode)
        {
            var code = new CodeNodeCall();
            code.span = astNode.Span();
            code.outputType = new TypePlaceholder();

            foreach (var child in astNode.children)
                code.children.Add(this.ParseExpression(child));

            return code;
        }


        private CodeNode ParseBinaryOp(Grammar.ASTNode astNode)
        {
            var op = astNode.Child(0).GetExcerpt();

            if (op == "=")
            {
                var code = new CodeNodeAssign();
                code.span = astNode.Span();

                code.children.Add(this.ParseExpression(astNode.Child(1)));
                code.children.Add(this.ParseExpression(astNode.Child(2)));
                code.outputType = new TypeTuple();

                return code;
            }
            else if (op == ".")
            {
                var code = new CodeNodeAccess();
                code.span = astNode.Span();

                code.children.Add(this.ParseExpression(astNode.Child(1)));
                code.outputType = new TypePlaceholder();

                if (!astNode.ChildIs(2, Grammar.ASTNodeKind.Name))
                {
                    this.session.diagn.Add(MessageKind.Error, MessageCode.Expected,
                        "expected a field name", astNode.Child(2).Span());
                    throw new CheckException();
                }

                code.pathASTNode = astNode.Child(2).Child(0);
                code.template = TemplateUtil.ResolveFromNameAST(this.session, astNode.Child(2), true);

                return code;
            }
            else
                throw new InternalException("not implemented");
        }


        private CodeNode ParseUnaryOp(Grammar.ASTNode astNode)
        {
            var op = astNode.Child(0).GetExcerpt();

            if (op == "&")
            {
                var code = new CodeNodeAddress();
                code.span = astNode.Span();
                code.children.Add(this.ParseExpression(astNode.Child(1)));
                code.outputType = new TypeReference(new TypePlaceholder());

                return code;
            }
            else if (op == "@")
            {
                var code = new CodeNodeDereference();
                code.span = astNode.Span();
                code.children.Add(this.ParseExpression(astNode.Child(1)));
                code.outputType = new TypePlaceholder();

                return code;
            }
            else
                throw new InternalException("not implemented");
        }


        private CodeNode ParseBooleanLiteral(Grammar.ASTNode astNode)
        {
            var code = new CodeNodeBooleanLiteral();
            code.span = astNode.Span();
            code.value = (astNode.GetExcerpt() == "true");
            code.outputType = new TypeStruct(this.session.primitiveBool);

            return code;
        }


        private CodeNode ParseNumberLiteral(Grammar.ASTNode astNode)
        {
            var code = new CodeNodeIntegerLiteral();
            code.span = astNode.Span();
            code.value = NumberUtil.ResolveValueFromAST(astNode);
            code.outputType = NumberUtil.ResolveTypeFromAST(this.session, astNode);

            return code;
        }


        private CodeNode ParseStructLiteral(Grammar.ASTNode astNode)
        {
            var code = new CodeNodeStructLiteral();
            code.span = astNode.Span();
            code.outputType = TypeUtil.ResolveFromAST(session, astNode.Child(0), true);

            var structType = (code.outputType as TypeStruct);
            if (structType == null)
            {
                this.session.diagn.Add(MessageKind.Error, MessageCode.Expected,
                    "expected a struct name", astNode.Child(0).Span());
                throw new CheckException();
            }

            var declStruct = structType.potentialStructs[0];
            if (declStruct.primitive)
            {
                this.session.diagn.Add(MessageKind.Error, MessageCode.Expected,
                    "using initializer syntax with primitive type", astNode.Child(0).Span());
                throw new CheckException();
            }

            var initChildIndices = new int[declStruct.fields.Count];
            for (int i = 0; i < declStruct.fields.Count; i++)
            {
                initChildIndices[i] = -1;
                code.children.Add(null);
            }

            var hadErrors = false;
            for (int i = 1; i < astNode.ChildNumber(); i++)
            {
                var fieldInit = astNode.Child(i);
                var fieldPathASTNode = fieldInit.Child(0).Child(0);
                var fieldTemplate = TemplateUtil.ResolveFromNameAST(this.session, fieldInit.Child(0), true);

                var fieldIndex = declStruct.FindField(fieldPathASTNode, fieldTemplate);
                if (fieldIndex < 0)
                {
                    this.session.diagn.Add(MessageKind.Error, MessageCode.UndeclaredTemplate,
                        "no field '" + NameUtil.GetDisplayString(fieldInit.Child(0)) + "' " +
                        "in '" + structType.GetString(this.session) + "'", fieldInit.Child(0).Span());
                    continue;
                }

                if (initChildIndices[fieldIndex] >= 0)
                {
                    this.session.diagn.Add(MessageKind.Error, MessageCode.UndeclaredTemplate,
                        "duplicate initializer for field '" + NameUtil.GetDisplayString(fieldInit.Child(0)) + "'",
                        fieldInit.Child(0).Span(), astNode.Child(initChildIndices[fieldIndex]).Child(0).Span());
                    continue;
                }

                initChildIndices[fieldIndex] = i;

                try
                {
                    var initExpr = this.ParseExpression(astNode.Child(i).Child(1));
                    code.children[fieldIndex] = initExpr;
                }
                catch (CheckException) { hadErrors = true; }
            }

            var firstNotInitialized = -1;
            var numNotInitialized = 0;
            for (int i = 0; i < initChildIndices.Length; i++)
            {
                if (initChildIndices[i] < 0)
                {
                    if (firstNotInitialized < 0)
                        firstNotInitialized = i;
                    numNotInitialized++;
                }
            }

            if (numNotInitialized > 0)
            {
                this.session.diagn.Add(MessageKind.Error, MessageCode.UndeclaredTemplate,
                    "missing initializer" + (numNotInitialized > 1 ? "s" : "") + " for field '" +
                    declStruct.fields[firstNotInitialized].name.GetString(this.session) +
                    "'" + (numNotInitialized == 1 ? "" : (" and other " + (numNotInitialized - 1))),
                    astNode.Span());
                throw new CheckException();
            }

            if (hadErrors)
                throw new CheckException();

            return code;
        }


        private CodeNode ParseName(Grammar.ASTNode astNode)
        {
            var templ = TemplateUtil.ResolveFromNameAST(this.session, astNode, false);

            var localIndex = -1;
            for (int i = this.localVariables.Count - 1; i >= 0; i--)
            {
                if (this.localVariablesInScope[i] &&
                    this.localVariables[i].name.Compare(astNode.Child(0), templ))
                {
                    localIndex = i;
                    break;
                }
            }

            if (localIndex >= 0)
            {
                var code = new CodeNodeLocal();
                code.span = astNode.Span();
                code.localIndex = localIndex;
                code.outputType = new TypePlaceholder();

                return code;
            }

            var functList = session.functDecls.GetDeclsClone(astNode.Child(0));
            if (functList.Count > 0)
            {
                var code = new CodeNodeFunct();
                code.span = astNode.Span();
                code.nameInference.pathASTNode = astNode.Child(0);
                code.nameInference.template = templ;
                code.potentialFuncts = functList;
                code.outputType = new TypePlaceholder();

                return code;
            }

            session.diagn.Add(MessageKind.Error, MessageCode.UndeclaredIdentifier,
                "'" + PathUtil.GetDisplayString(astNode.Child(0)) + "' is not declared",
                astNode.Span());
            throw new CheckException();
        }
    }
}
