﻿using System.Collections.Generic;


namespace Trapl.Semantics
{
    public abstract class Type
    {
        public bool addressable;

        public virtual Type Clone() { return (Type)MemberwiseClone(); }

        public virtual bool IsSame(Type other) { return false; }

        public virtual string GetString(Infrastructure.Session session) { return "(???)"; }
    }


    public class TypeError : Type
    {
        public override string GetString(Infrastructure.Session session) { return "(error)"; }
    }


    public class TypeUnconstrained : Type
    {
        public override string GetString(Infrastructure.Session session) { return "(unconstrained)"; }
    }


    public class TypePointer : Type
    {
        public Type pointeeType;

        public TypePointer(Type pointeeType) { this.pointeeType = pointeeType; }
        public override bool IsSame(Type other)
        {
            if (!(other is TypePointer)) return false;
            return (((TypePointer)other).pointeeType.IsSame(this.pointeeType));
        }

        public override string GetString(Infrastructure.Session session)
        {
            return "&" + pointeeType.GetString(session);
        }
    }


    public class TypeStruct : Type
    {
        public DefStruct structDef;

        public TypeStruct(DefStruct structDef) { this.structDef = structDef; }
        public override bool IsSame(Type other)
        {
            if (!(other is TypeStruct)) return false;
            return (((TypeStruct)other).structDef == this.structDef);
        }
        public override string GetString(Infrastructure.Session session)
        { 
            foreach (var topDecl in session.topDecls)
            {
                if (structDef == topDecl.def)
                {
                    return topDecl.GetString(session);
                }
            }
            return "<unknown>";
        }
    }

    public class TypeTuple : Type
    {
        public List<Type> elementTypes = new List<Type>();

        public override bool IsSame(Type other)
        {
            var otherf = other as TypeTuple;
            if (otherf == null) return false;
            if (this.elementTypes.Count != otherf.elementTypes.Count) return false;
            for (int i = 0; i < elementTypes.Count; i++)
                if (!this.elementTypes[i].IsSame(otherf.elementTypes[i])) return false;
            return true;
        }
        public override string GetString(Infrastructure.Session session)
        {
            var result = "(";
            for (int i = 0; i < elementTypes.Count; i++)
            {
                result += elementTypes[i].GetString(session);
                if (i < elementTypes.Count - 1)
                    result += ", ";
            }
            return result + ")";
        }
    }


    public class TypeFunct : Type
    {
        public List<Type> argumentTypes = new List<Type>();
        public Type returnType;


        public TypeFunct() { }

        public TypeFunct(DefFunct f)
        {
            for (int i = 0; i < f.arguments.Count; i++)
            {
                this.argumentTypes.Add(f.arguments[i].type);
            }
            this.returnType = f.returnType;
        }

        public override bool IsSame(Type other)
        {
            var otherf = other as TypeFunct;
            if (otherf == null) return false;
            if (this.argumentTypes.Count != otherf.argumentTypes.Count) return false;
            for (int i = 0; i < argumentTypes.Count; i++)
                if (!this.argumentTypes[i].IsSame(otherf.argumentTypes[i])) return false;
            return true;
        }
        public override string GetString(Infrastructure.Session session)
        {
            var result = "(";
            for (int i = 0; i < argumentTypes.Count; i++)
            {
                result += argumentTypes[i].GetString(session);
                if (i < argumentTypes.Count - 1)
                    result += ", ";
            }
            return result + ") -> " + returnType.GetString(session);
        }
    }
}
