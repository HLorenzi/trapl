﻿using System;
using Trapl.Diagnostics;


namespace Trapl.Semantics
{
    public class CheckTopDecl
    {
        public static void Check(Interface.Session session, Grammar.AST ast, Interface.SourceCode source)
        {
            AddPrimitive(session, "Void");
            AddPrimitive(session, "Bool");
            AddPrimitive(session, "Int8");
            AddPrimitive(session, "Int16");
            AddPrimitive(session, "Int32");
            AddPrimitive(session, "Int64");
            AddPrimitive(session, "UInt8");
            AddPrimitive(session, "UInt16");
            AddPrimitive(session, "UInt32");
            AddPrimitive(session, "UInt64");
            AddPrimitive(session, "Float32");
            AddPrimitive(session, "Float64");

            foreach (var node in ast.topDecls)
            {
                try
                {
                    EnsureKind(session, node, Grammar.ASTNodeKind.TopLevelDecl, source);
                    EnsureKind(session, node.Child(0), Grammar.ASTNodeKind.Identifier, source);
                    EnsureKind(session, node.Child(0).Child(0), Grammar.ASTNodeKind.Name, source);

                    var qualifiedNameNode = node.Child(0).Child(0);
                    var qualifiedName = source.GetExcerpt(qualifiedNameNode.Span());

                    var genericPatternNode = new Grammar.ASTNode(Grammar.ASTNodeKind.GenericPattern);
                    var genericPattern = new DeclPattern(source, genericPatternNode);

                    if (node.Child(0).ChildIs(1, Grammar.ASTNodeKind.GenericPattern) ||
                        node.Child(0).ChildIs(1, Grammar.ASTNodeKind.VariadicGenericPattern))
                    {
                        genericPatternNode = node.Child(0).Child(1);
                        genericPattern.SetPattern(genericPatternNode);
                    }

                    var defNode = node.Child(1);

                    var topDecl = new TopDecl();
                    topDecl.source = source;
                    topDecl.qualifiedName = qualifiedName;
                    topDecl.qualifiedNameASTNode = qualifiedNameNode;
                    topDecl.pattern = genericPattern;
                    topDecl.patternASTNode = genericPatternNode;
                    topDecl.generic = genericPattern.IsGeneric();
                    topDecl.defASTNode = defNode;
                    session.topDecls.Add(topDecl);

                    if (defNode.kind != Grammar.ASTNodeKind.StructDecl)
                        throw ErrorAt(session, "Decl", defNode, source);
                }
                catch (CheckException) { }
            }
        }


        private static void AddPrimitive(Interface.Session session, string name)
        {
            var topDecl = new TopDecl();
            topDecl.source = null;
            topDecl.qualifiedName = name;
            topDecl.qualifiedNameASTNode = null;
            topDecl.pattern = new DeclPattern(null, new Grammar.ASTNode(Grammar.ASTNodeKind.GenericPattern));
            topDecl.patternASTNode = null;
            topDecl.defASTNode = null;
            topDecl.def = new DefStruct();
            topDecl.resolved = true;
            session.topDecls.Add(topDecl);
        }


        private static void EnsureKind(Interface.Session session, Grammar.ASTNode node, Grammar.ASTNodeKind kind, Interface.SourceCode source)
        {
            if (node.kind != kind)
                throw ErrorAt(session, Enum.GetName(typeof(Grammar.ASTNodeKind), kind), node, source);
        }


        private static CheckException ErrorAt(Interface.Session session, string msg, Grammar.ASTNode node, Interface.SourceCode source)
        {
            session.diagn.Add(MessageKind.Error, MessageCode.Internal, "expecting '" + msg + "' node", source, node.Span());
            return new CheckException();
        }
    }
}
