﻿using System;
using System.Collections.Generic;
using System.Text;

namespace D_Parser
{
    public abstract class TypeDeclaration
    {
        public abstract uint GetDeclarationClassTypeId { get; }

        public TypeDeclaration Base=null;
        public TypeDeclaration ParentDecl { get { return Base; } set { Base = value; } }

        public TypeDeclaration MostBasic
        {
            get {
                if (Base != null) return Base.MostBasic;
                else return Base;
            }
            set
            {
                if (Base != null) Base.MostBasic=value;
                else Base=value;
            }
        }

        public TypeDeclaration OneBeforeMostBasic
        {
            set {
                if (Base.Base == null) Base = value;
                else Base.OneBeforeMostBasic = value;
            }
        }

        /// <summary>
        /// Returns a string which represents the current type
        /// </summary>
        /// <returns></returns>
        public new abstract string ToString();
    }

    /// <summary>
    /// Basic type, e.g. &gt;int&lt;
    /// </summary>
    public class NormalDeclaration : TypeDeclaration
    {
        public override uint GetDeclarationClassTypeId { get { return 1; } }
        public string Name;
        /// <summary>
        /// Unused
        /// </summary>
        public new TypeDeclaration Base;

        public NormalDeclaration() { }
        public NormalDeclaration(string Identifier)
        { Name = Identifier; }

        public override string  ToString()
        {
            return Name;
        }
    }

    public class DTokenDeclaration : NormalDeclaration
    {
        public override uint GetDeclarationClassTypeId { get { return 2; } }
        public int Token;
        /// <summary>
        /// Unused
        /// </summary>
        public new TypeDeclaration Base;

        public DTokenDeclaration() { }
        public DTokenDeclaration(int Token)
        { this.Token = Token; }

        public new string Name
        {
            get { return DTokens.GetTokenString(Token); }
            set { Token = DTokens.GetTokenID(value); }
        }

        public override string ToString()
        {
            return Name;
        }
    }

    /// <summary>
    /// Array decl, e.g. &gt;int[string]&lt; myArray;
    /// </summary>
    public class ArrayDecl : TypeDeclaration
    {
        public override uint GetDeclarationClassTypeId { get { return 3; } }
        public TypeDeclaration ValueType
        {
            get { return Base; }
            set { Base = value; }
        }
        public TypeDeclaration KeyType;

        public ArrayDecl() { }
        public ArrayDecl(TypeDeclaration ValueType) { this.ValueType = ValueType; }

        public override string ToString()
        {
            return ValueType.ToString()+"["+KeyType.ToString()+"]";
        }
    }

    public class DelegateDeclaration : TypeDeclaration
    {
        public override uint GetDeclarationClassTypeId { get { return 4; } }
        public TypeDeclaration ReturnType
        {
            get { return Base; }
            set { Base = value; }
        }
        /// <summary>
        /// Is it a function(), not a delegate() ?
        /// </summary>
        public bool IsFunction = false;

        public List<DVariable> Parameters = new List<DVariable>();

        public override string ToString()
        {
            string ret=ReturnType.ToString()+(IsFunction?" function":" delegate")+"(";

            foreach (DVariable n in Parameters)
            {
                ret += n.Type.ToString()+" "+n.name+ (String.IsNullOrEmpty(n.Value)?"":("= "+n.Value))+", ";
            }
            ret=ret.TrimEnd(',',' ')+")";
            return ret;
        }
    }

    /// <summary>
    /// int* ptr;
    /// </summary>
    public class PointerDecl : TypeDeclaration
    {
        public override uint GetDeclarationClassTypeId { get { return 5; } }
        public PointerDecl() { }
        public PointerDecl(TypeDeclaration BaseType) { Base = BaseType; }

        public override string ToString()
        {
            return Base.ToString() + "*";
        }
    }

    /// <summary>
    /// const(char)
    /// </summary>
    public class MemberFunctionAttributeDecl : DTokenDeclaration
    {
        public override uint GetDeclarationClassTypeId { get { return 6; } }
        public int Modifier
        {
            get { return Token; }
            set { Token = value; }
        }

        public MemberFunctionAttributeDecl() { }
        public MemberFunctionAttributeDecl(int ModifierToken) { this.Modifier = ModifierToken; }

        public override string ToString()
        {
            return Name+"("+Base.ToString()+")";
        }
    }

    public class VarArgDecl : NormalDeclaration
    {
        public override uint GetDeclarationClassTypeId { get { return 7; } }
        public VarArgDecl() { }
        public VarArgDecl(string Identifier) { Name = Identifier; }

        public override string ToString()
        {
            return Name+"...";
        }
    }

    // Secondary importance
    /// <summary>
    /// class ABC: &gt;A, C&lt;
    /// </summary>
    public class InheritanceDecl : TypeDeclaration
    {
        public override uint GetDeclarationClassTypeId { get { return 8; } }
        public TypeDeclaration InheritedClass;
        public TypeDeclaration InheritedInterface;

        public InheritanceDecl() { }
        public InheritanceDecl(TypeDeclaration Base) { this.Base = Base; }

        public override string ToString()
        {
            return Base.ToString()+": "+InheritedClass.ToString()+(InheritedInterface!=null?(", "+InheritedInterface.ToString()):"");
        }
    }

    /// <summary>
    /// List&lt;T:base&gt; myList;
    /// </summary>
    public class TemplateDecl : TypeDeclaration
    {
        public override uint GetDeclarationClassTypeId { get { return 9; } }
        public TypeDeclaration Template;

        public TemplateDecl() { }
        public TemplateDecl(TypeDeclaration Base)
        {
            this.Base = Base;
        }

        public override string ToString()
        {
            return Base.ToString()+"("+Template.ToString()+")";
        }
    }

    /// <summary>
    /// A.B
    /// </summary>
    public class DotCombinedDeclaration : TypeDeclaration
    {
        public override uint GetDeclarationClassTypeId { get { return 10; } }
        public TypeDeclaration AccessedMember;

        public DotCombinedDeclaration() { }
        public DotCombinedDeclaration(TypeDeclaration Base) { this.Base = Base; }

        public override string ToString()
        {
            return Base.ToString()+"."+AccessedMember.ToString();
        }
    }
}