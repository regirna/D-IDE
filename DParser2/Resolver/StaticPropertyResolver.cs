﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;
using D_Parser.Dom;

namespace D_Parser.Resolver
{
	public class StaticPropertyResolver
	{
		/// <summary>
		/// Tries to resolve a static property's name.
		/// Returns a result describing the theoretical member (".init"-%gt;MemberResult; ".typeof"-&gt;TypeResult etc).
		/// Returns null if nothing was found.
		/// </summary>
		/// <param name="InitialResult"></param>
		/// <returns></returns>
		public static ResolveResult TryResolveStaticProperties(ResolveResult InitialResult, string propertyIdentifier, ResolverContextStack ctxt = null, IdentifierDeclaration idContainter = null)
		{
			if (InitialResult == null || InitialResult is ModuleResult)
				return null;

			INode relatedNode = null;

			if (InitialResult is MemberResult)
				relatedNode = (InitialResult as MemberResult).ResolvedMember;
			else if (InitialResult is TypeResult)
				relatedNode = (InitialResult as TypeResult).ResolvedTypeDefinition;

			#region init
			if (propertyIdentifier == "init")
			{
				var prop_Init = new DVariable
				{
					Name = "init",
					Description = "Initializer"
				};

				if (relatedNode != null)
				{
					if (!(relatedNode is DVariable))
					{
						prop_Init.Parent = relatedNode.Parent;
						prop_Init.Type = new IdentifierDeclaration(relatedNode.Name);
					}
					else
					{
						prop_Init.Parent = relatedNode;
						prop_Init.Initializer = (relatedNode as DVariable).Initializer;
						prop_Init.Type = relatedNode.Type;
					}
				}

				return new MemberResult
				{
					ResultBase = InitialResult,
					MemberBaseTypes = new[] { InitialResult },
					DeclarationOrExpressionBase = idContainter,
					ResolvedMember = prop_Init
				};
			}
			#endregion

			#region sizeof
			if (propertyIdentifier == "sizeof")
				return new MemberResult
				{
					ResultBase = InitialResult,
					DeclarationOrExpressionBase = idContainter,
					ResolvedMember = new DVariable
					{
						Name = "sizeof",
						Type = new DTokenDeclaration(DTokens.Int),
						Initializer = new IdentifierExpression(4),
						Description = "Size in bytes (equivalent to C's sizeof(type))"
					}
				};
			#endregion

			#region alignof
			if (propertyIdentifier == "alignof")
				return new MemberResult
				{
					ResultBase = InitialResult,
					DeclarationOrExpressionBase = idContainter,
					ResolvedMember = new DVariable
					{
						Name = "alignof",
						Type = new DTokenDeclaration(DTokens.Int),
						Description = "Alignment size"
					}
				};
			#endregion

			#region mangleof
			if (propertyIdentifier == "mangleof")
				return new MemberResult
				{
					ResultBase = InitialResult,
					DeclarationOrExpressionBase = idContainter,
					ResolvedMember = new DVariable
					{
						Name = "mangleof",
						Type = new IdentifierDeclaration("string"),
						Description = "String representing the ‘mangled’ representation of the type"
					},
					MemberBaseTypes = TypeDeclarationResolver.Resolve(new IdentifierDeclaration("string"), ctxt)
				};
			#endregion

			#region stringof
			if (propertyIdentifier == "stringof")
				return new MemberResult
				{
					ResultBase = InitialResult,
					DeclarationOrExpressionBase = idContainter,
					ResolvedMember = new DVariable
					{
						Name = "stringof",
						Type = new IdentifierDeclaration("string"),
						Description = "String representing the source representation of the type"
					},
					MemberBaseTypes = TypeDeclarationResolver.Resolve(new IdentifierDeclaration("string"), ctxt)
				};
			#endregion

			bool
				isArray= false,
				isAssocArray = false,
				isInt = false,
				isFloat = false;

			if (InitialResult is StaticTypeResult)
			{
				var srr = InitialResult as StaticTypeResult;

				var type = srr.DeclarationOrExpressionBase;

				// on things like immutable(char), pass by the surrounding attribute..
				while (type is MemberFunctionAttributeDecl)
					type = (type as MemberFunctionAttributeDecl).InnerType;

				if (!(type is PointerDecl))
				{
					int TypeToken = srr.BaseTypeToken;

					if (TypeToken <= 0 && type is DTokenDeclaration)
						TypeToken = (type as DTokenDeclaration).Token;

					if (TypeToken > 0)
					{
						// Determine whether float by the var's base type
						isInt = DTokens.BasicTypes_Integral[srr.BaseTypeToken];
						isFloat = DTokens.BasicTypes_FloatingPoint[srr.BaseTypeToken];
					}
				}
			}
			else if (InitialResult is ArrayResult)
			{
				isArray=true;

				var ar = InitialResult as ArrayResult;

				isAssocArray = ar.ArrayDeclaration.IsAssociative;
			}			

			// .classinfo property, too!

			return null;
		}
	}
}
