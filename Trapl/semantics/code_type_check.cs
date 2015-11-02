﻿using Trapl.Diagnostics;


namespace Trapl.Semantics
{
    public class CodeTypeChecker
    {
        public static void Check(Infrastructure.Session session, CodeBody body)
        {
            var checker = new CodeTypeChecker(session, body);
            checker.Check();
        }


        private Infrastructure.Session session;
        private CodeBody body;


        private CodeTypeChecker(Infrastructure.Session session, CodeBody body)
        {
            this.session = session;
            this.body = body;
        }


        private void Check()
        {
            this.CheckUnresolvedLocals();
            this.PerformCheck(CheckAssignment, this.body.code);
            this.PerformCheck(CheckFunctResolution, this.body.code);
            this.PerformCheck(CheckCallArguments, this.body.code);
        }


        private delegate void RuleDelegate(CodeNode code);


        private void PerformCheck(RuleDelegate rule, CodeNode node)
        {
            rule(node);
            foreach (var child in node.children)
                this.PerformCheck(rule, child);
        }


        private bool DoesMismatch(Type type1, Type type2)
        {
            if (type1 is TypeUnconstrained ||
                type2 is TypeUnconstrained ||
                type1 is TypeError ||
                type2 is TypeError)
                return false;

            return !type1.IsSame(type2);
        }


        private void CheckUnresolvedLocals()
        {
            foreach (var loc in this.body.localVariables)
            {
                if (!loc.type.IsResolved())
                {
                    session.diagn.Add(MessageKind.Error, MessageCode.InferenceFailed,
                        "cannot infer type for '" +
                        loc.GetString(this.session) + "'",
                        loc.declSpan);

                    loc.type = new TypeError();
                }
            }
        }

        private void CheckAssignment(CodeNode code)
        {
            var codeAssign = code as CodeNodeAssign;
            if (codeAssign == null)
                return;

            if (DoesMismatch(codeAssign.children[0].outputType, codeAssign.children[1].outputType))
            {
                session.diagn.Add(MessageKind.Error, MessageCode.IncompatibleTypes,
                    "assigning '" + codeAssign.children[1].outputType.GetString(session) + "' " +
                    "to '" + codeAssign.children[0].outputType.GetString(session) + "'",
                    codeAssign.children[0].span,
                    codeAssign.children[1].span);
            }
        }

        private void CheckFunctResolution(CodeNode code)
        {
            var codeFunct = code as CodeNodeFunct;
            if (codeFunct == null)
                return;

            if (codeFunct.potentialFuncts.Count > 1)
            {
                session.diagn.Add(MessageKind.Error, MessageCode.InferenceFailed,
                    "cannot infer which '" + PathASTUtil.GetString(codeFunct.nameInference.pathASTNode) +
                    "' declaration to use",
                    codeFunct.span);
                session.diagn.AddInnerToLast(MessageKind.Info, MessageCode.Info,
                    "ambiguous between the following declarations" +
                    (codeFunct.potentialFuncts.Count > 2 ? " and other " + (codeFunct.potentialFuncts.Count - 2) : ""),
                    codeFunct.potentialFuncts[0].nameASTNode.Span(),
                    codeFunct.potentialFuncts[1].nameASTNode.Span());
            }
            else if (codeFunct.potentialFuncts.Count == 0)
            {
                if (codeFunct.nameInference.template.IsFullyResolved())
                {
                    session.diagn.Add(MessageKind.Error, MessageCode.UndeclaredTemplate,
                        "no '" + PathASTUtil.GetString(codeFunct.nameInference.pathASTNode) +
                        "' declaration accepts this template",
                        codeFunct.span);
                }
                else
                {
                    session.diagn.Add(MessageKind.Error, MessageCode.InferenceFailed,
                        "cannot infer which '" + PathASTUtil.GetString(codeFunct.nameInference.pathASTNode) +
                        "' declaration to use",
                        codeFunct.span);
                }
            }
        }

        private void CheckCallArguments(CodeNode code)
        {
            var codeCall = code as CodeNodeCall;
            if (codeCall == null)
                return;

            var functType = codeCall.children[0].outputType as TypeFunct;

            if (functType == null)
            {
                if (codeCall.children[0].outputType.IsResolved())
                {
                    session.diagn.Add(MessageKind.Error, MessageCode.InferenceFailed,
                        "'" + codeCall.children[0].outputType.GetString(this.session) + "' " +
                        "is not callable",
                        codeCall.children[0].span);
                }

                return;
            }

            for (var i = 0; i < codeCall.children.Count - 1; i++)
            {
                if (functType.argumentTypes[i].IsResolved() &&
                    !functType.argumentTypes[i].IsSame(codeCall.children[i + 1].outputType))
                {
                    session.diagn.Add(MessageKind.Error, MessageCode.InferenceFailed,
                        "passing '" + codeCall.children[i + 1].outputType.GetString(this.session) +
                        "' to '" + functType.argumentTypes[i].GetString(this.session) + "' argument",
                        codeCall.children[i + 1].span);
                }
            }
        }
    }
}
