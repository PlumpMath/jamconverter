﻿using System;
using System.Collections.Generic;
using System.Linq;
using jamconverter.AST;
using runtimelib;
using NRefactory = ICSharpCode.NRefactory.CSharp;

namespace jamconverter
{
    class JamToCSharpConverter
    {
        private NRefactory.SyntaxTree _syntaxTree;
        private NRefactory.TypeDeclaration _dummyType;
        private NRefactory.MethodDeclaration _mainMethod;
        private NRefactory.TypeDeclaration _staticGlobals;

        public string Convert(string simpleProgram)
        {
            _syntaxTree = new NRefactory.SyntaxTree();
            _syntaxTree.Members.Add(new NRefactory.UsingDeclaration("System"));
            _syntaxTree.Members.Add(new NRefactory.UsingDeclaration("System.Linq"));
			_syntaxTree.Members.Add(new NRefactory.UsingDeclaration("runtimelib"));
			_syntaxTree.Members.Add(new NRefactory.UsingDeclaration("static BuiltinFunctions"));
            _dummyType = new NRefactory.TypeDeclaration {Name = "Dummy"};
            _syntaxTree.Members.Add(_dummyType);

            _staticGlobals = new NRefactory.TypeDeclaration {Name = "StaticGlobals", BaseTypes = { new NRefactory.SimpleType("GlobalVariables") }};
            _syntaxTree.Members.Add(_staticGlobals);

             _dummyType.Members.Add(new NRefactory.FieldDeclaration() {ReturnType = StaticGlobalsType, Modifiers = NRefactory.Modifiers.Static, Variables = {new NRefactory.VariableInitializer("Globals", new NRefactory.ObjectCreateExpression(StaticGlobalsType))}});

			var variable = new NRefactory.VariableInitializer("_dynamicRuleInvocationService", new NRefactory.ObjectCreateExpression(DynamicRuleInvocationServiceType, new NRefactory.TypeOfExpression(new NRefactory.SimpleType("Dummy"))));
			_dummyType.Members.Add(new NRefactory.FieldDeclaration() { ReturnType = DynamicRuleInvocationServiceType, Modifiers = NRefactory.Modifiers.Static, Variables = {variable} });

			var parser = new Parser(simpleProgram);
            var body = new NRefactory.BlockStatement();
            while (true)
            {
                var statement = parser.ParseStatement();
                if (statement == null)
                    break;

                var nStatement = ProcessStatement(statement);
                if (nStatement != null)
                    body.Statements.Add(nStatement);
            }
            
            _mainMethod = new NRefactory.MethodDeclaration { Name = "Main", ReturnType = new NRefactory.PrimitiveType("void"), Modifiers = NRefactory.Modifiers.Static, Body = body};
            _dummyType.Members.Add(_mainMethod);

            return _syntaxTree.ToString();
        }

	    private static NRefactory.SimpleType DynamicRuleInvocationServiceType
	    {
		    get { return new NRefactory.SimpleType(nameof(DynamicRuleInvocationService)); }
	    }

	    private NRefactory.SimpleType StaticGlobalsType => new NRefactory.SimpleType(_staticGlobals.Name);

        private NRefactory.Statement ProcessStatement(Statement statement)
        {
            if (statement == null)
                return null;

            if (statement is IfStatement)
                return ProcessIfStatement((IfStatement) statement);

            if (statement is WhileStatement)
                return ProcessWhileStatement((WhileStatement) statement);

            if (statement is RuleDeclarationStatement)
            {
                ProcessRuleDeclarationStatement((RuleDeclarationStatement) statement);
                return null;
            }

            if (statement is ReturnStatement)
                return new NRefactory.ReturnStatement(ProcessExpressionList(statement.As<ReturnStatement>().ReturnExpression, mightModify:true));

            if (statement is ForStatement)
                return ProcessForStatement((ForStatement) statement);

            if (statement is BreakStatement)
                return new NRefactory.BreakStatement();

            if (statement is ContinueStatement)
                return new NRefactory.ContinueStatement();

            if (statement is BlockStatement)
                return ProcessBlockStatement((BlockStatement) statement);

            if (statement is SwitchStatement)
                return ProcessSwitchStatement((SwitchStatement) statement);

	        if (statement is LocalStatement)
		        return ProcessLocalStatement((LocalStatement) statement);

	        if (statement is ActionsDeclarationStatement)
		        return ProcessActionsDeclarationStatement((ActionsDeclarationStatement) statement);
			
			if (statement is AssignmentStatement)
			{
				AssignmentStatement assignmentStatement = (AssignmentStatement)statement;
				return ProcessAssignment(assignmentStatement.Left, assignmentStatement.Operator, assignmentStatement.Right);
			}

	        if (statement is OnStatement)
		        return ProcessOnStatement((OnStatement) statement);

	        return ProcessExpressionStatement((ExpressionStatement) statement);
        }

	    private NRefactory.Statement ProcessOnStatement(OnStatement statement)
	    {
		    var onStartContextMethod = new NRefactory.MemberReferenceExpression(new NRefactory.IdentifierExpression("Globals"), "OnTargetContext");

		    var onStartContextInvocation = new NRefactory.InvocationExpression(onStartContextMethod, ProcessExpression(statement.Target));
			
		    return new NRefactory.UsingStatement
		    {
			    ResourceAcquisition = onStartContextInvocation,
			    EmbeddedStatement = ProcessStatement(statement.Body)
		    };
		}

	    private NRefactory.Statement ProcessActionsDeclarationStatement(ActionsDeclarationStatement statement)
	    {
		    return new NRefactory.IdentifierExpression("ActionsDeclarationStamentTODO");
	    }
    
	    private NRefactory.Statement ProcessLocalStatement(LocalStatement statement)
	    {
			return ProcessAssignment(statement.Variable, Operator.Assignment, statement.Value);
		}

	    private NRefactory.SwitchStatement ProcessSwitchStatement(SwitchStatement switchStatement)
        {
            var invocationExpression = new NRefactory.InvocationExpression(new NRefactory.IdentifierExpression("SwitchTokenFor"), ProcessExpression(switchStatement.Variable));
            var result = new NRefactory.SwitchStatement() {Expression = invocationExpression};
           
            foreach(var switchCase in switchStatement.Cases)
            {
                var section = new NRefactory.SwitchSection();
                section.CaseLabels.Add(new NRefactory.CaseLabel(new NRefactory.PrimitiveExpression(switchCase.CaseExpression.Value)));
                section.Statements.AddRange(switchCase.Statements.Select(ProcessStatement));
                section.Statements.Add(new NRefactory.BreakStatement());
                result.SwitchSections.Add(section);
            }
            return result;
        }
        
        private NRefactory.ForeachStatement ProcessForStatement(ForStatement statement)
        {
            return new NRefactory.ForeachStatement
            {
                VariableType = JamListAstType,
                VariableName = statement.LoopVariable.Value,
                InExpression = new NRefactory.MemberReferenceExpression(ProcessExpressionList(statement.List),"ElementsAsJamLists"),
                EmbeddedStatement = ProcessStatement(statement.Body)
            };
        }

        private NRefactory.ExpressionStatement ProcessExpressionStatement(ExpressionStatement expressionStatement)
        {
            if (expressionStatement.Expression is InvocationExpression)
                return new NRefactory.ExpressionStatement(ProcessExpression(expressionStatement.Expression));
	
            throw new ArgumentException("Unsupported node: " + expressionStatement.Expression);
        }

	    private NRefactory.Statement ProcessAssignment(Expression left, Operator @operator, NodeList<Expression> right)
	    {
		    var csharpMethodNameForAssignmentOperator = CSharpMethodNameForAssignmentOperator(@operator);
		    var memberReferenceExpression = new NRefactory.MemberReferenceExpression(ProcessExpressionForLeftHandOfAssignment(left),
			    csharpMethodNameForAssignmentOperator);
		    var processExpression = ExpressionsForJamListConstruction(right);
		    return new NRefactory.InvocationExpression(memberReferenceExpression, processExpression);
	    }

	    private NRefactory.Expression ProcessExpressionForLeftHandOfAssignment(Expression left)
	    {
			//lefthandside:
			//mads = 2				         ->  Globals.mads.Assign("2");
			//mads_arg = 2				   ->  mads_arg.Assign("2");
			//$(mads) = 2					->  Globals.DereferenceElements(Globals.mads).Assign("2");
			//$($(mads)) = 2				->  Globals.DereferenceElements().DerefenceElements().Assign("2");
			//mads on mytarget = 2			->  Globals.GetOrCreateVariableOnTargetContext("mytarget", "mads").Assign(2);
			//$(mads) on mytarget = 2		->  Globals.GetOrCreateVariableOnTargetContext("mytarget", Globals.mads).Assign(2);
			//$(mads) on $(mytargets) = 2	->  Globals.GetOrCreateVariableOnTargetContext(Globals.mytargets, Globals.mads).Assign(2);
			var literalExpression = left as LiteralExpression;
		    if (literalExpression != null)
			    return ProcessIdentifier(left, literalExpression.Value);

		    var deref = left as VariableDereferenceExpression;
		    if (deref != null)
			    return DereferenceElementsNonFlatInvocationFor(ProcessExpressionForLeftHandOfAssignment(deref.VariableExpression));

		    var variableOnTargetExpression = left as VariableOnTargetExpression;
		    if (variableOnTargetExpression != null)
		    {
			    var variableExpression = ProcessExpression(variableOnTargetExpression.Variable);
			    var targetExpression = ProcessExpressionList(variableOnTargetExpression.Targets);
				return new NRefactory.InvocationExpression(new NRefactory.MemberReferenceExpression(new NRefactory.IdentifierExpression("Globals"), "GetOrCreateVariableOnTargetContext"), targetExpression, variableExpression);
			}

		    var combineExpression = left as CombineExpression;
		    if (combineExpression != null)
			    return DereferenceElementsNonFlatInvocationFor(ProcessExpression(combineExpression));

		    throw new NotImplementedException();
	    }

	    private NRefactory.InvocationExpression DereferenceElementsNonFlatInvocationFor(NRefactory.Expression expression)
	    {
		    return new NRefactory.InvocationExpression(new NRefactory.MemberReferenceExpression(new NRefactory.IdentifierExpression("Globals"), "DereferenceElementsNonFlat"), expression);
	    }

	    private NRefactory.Expression ProcessIdentifier(Expression parentExpression, string identifierName)
	    {
		    var cleanName = CleanIllegalCharacters(identifierName);

		    if (parentExpression != null)
		    {
			    var parentRule = FindParentOfType<RuleDeclarationStatement>(parentExpression);
			    if (parentRule != null && parentRule.Arguments.Contains(identifierName))
				    return new NRefactory.IdentifierExpression(cleanName);

			    var forLoop = FindParentOfType<ForStatement>(parentExpression);
			    if (forLoop != null && forLoop.LoopVariable.Value == identifierName)
				    return new NRefactory.IdentifierExpression(cleanName);
		    }

		    return StaticGlobalVariableFor(cleanName);
	    }

	    private NRefactory.MemberReferenceExpression StaticGlobalVariableFor(string cleanName)
        {
            if (_staticGlobals.Members.OfType<NRefactory.PropertyDeclaration>().All(p => p.Name != cleanName))
            {
                var indexerExpression = new NRefactory.IndexerExpression(new NRefactory.ThisReferenceExpression(), new NRefactory.PrimitiveExpression(cleanName));
                _staticGlobals.Members.Add(new NRefactory.PropertyDeclaration()
                {
                    Name = cleanName,
                    ReturnType = JamListAstType,
                    Modifiers = NRefactory.Modifiers.Public,
                    Getter = new NRefactory.Accessor() {Body = new NRefactory.BlockStatement() {Statements = {new NRefactory.ReturnStatement(indexerExpression.Clone())}}},
                    Setter = new NRefactory.Accessor() {Body = new NRefactory.BlockStatement() { Statements = {new NRefactory.AssignmentExpression(indexerExpression.Clone(), new NRefactory.IdentifierExpression("value"))}} }
                });
            }
            return new NRefactory.MemberReferenceExpression(new NRefactory.IdentifierExpression("Globals"), cleanName);
        }

        private T FindParentOfType<T>(Node node) where T : Node
        {
            if (node.Parent == null)
                return null;

            var needleNode = node.Parent as T;
            if (needleNode != null)
                return needleNode;

            return FindParentOfType<T>(node.Parent);
        }

        private static string CSharpMethodNameForAssignmentOperator(Operator assignmentOperator)
        {
            switch (assignmentOperator)
            {
				case Operator.Assignment:
		            return "Assign";
                case Operator.Append:
                    return "Append";
                case Operator.Subtract:
                    return "Subtract";
				case Operator.AssignmentIfEmpty:
		            return "AssignIfEmpty";
                default:
                    throw new NotSupportedException("Unsupported operator in assignment: " + assignmentOperator);
            }
        }

        private void ProcessRuleDeclarationStatement(RuleDeclarationStatement ruleDeclaration)
        {
            //because the parser always interpets an invocation without any arguments as one with a single argument: an empty expressionlist,  let's make sure we always are ready to take a single argument
            var arguments = ruleDeclaration.Arguments.Length == 0 ? new[] { "dummyArgument" } : ruleDeclaration.Arguments;

            var body = new NRefactory.BlockStatement();

            var processRuleDeclarationStatement = new NRefactory.MethodDeclaration()
            {
                Name = MethodNameFor(ruleDeclaration),
                ReturnType = JamListAstType,
                Modifiers = NRefactory.Modifiers.Static,
                Body = body
            };
            processRuleDeclarationStatement.Parameters.AddRange(arguments.Select(a => new NRefactory.ParameterDeclaration(JamListAstType, ArgumentNameFor(a))));

	        foreach (var arg in arguments)
	        {
				var identifier = new NRefactory.IdentifierExpression(ArgumentNameFor(arg));
		        var cloneExpression = new NRefactory.InvocationExpression(new NRefactory.MemberReferenceExpression(identifier, "Clone")); 
		        body.Statements.Add(new NRefactory.ExpressionStatement(new NRefactory.AssignmentExpression(identifier.Clone(), cloneExpression )));
	        }
			
            foreach (var subStatement in ruleDeclaration.Body.Statements)
                body.Statements.Add(ProcessStatement(subStatement));

            if (!DoesBodyEndWithReturnStatement(ruleDeclaration))
                body.Statements.Add(new NRefactory.ReturnStatement(new NRefactory.NullReferenceExpression()));
            
            _dummyType.Members.Add(processRuleDeclarationStatement);
        }

	    private static bool DoesBodyEndWithReturnStatement(RuleDeclarationStatement ruleDeclaration)
	    {
		    var statements = ruleDeclaration.Body.Statements;
		    if (statements.Length == 0)
			    return false;
		    return statements.Last() is ReturnStatement;
	    }

	    public static NRefactory.AstType JamListAstType => new NRefactory.SimpleType("JamList");

        private NRefactory.IfElseStatement ProcessIfStatement(IfStatement ifStatement)
        {
            return new NRefactory.IfElseStatement(ProcessCondition(ifStatement.Condition), ProcessStatement(ifStatement.Body), ProcessStatement(ifStatement.Else));
        }
        
        private NRefactory.BlockStatement ProcessBlockStatement(BlockStatement blockStatement)
        {
            var processBlockStatement = new NRefactory.BlockStatement();
            processBlockStatement.Statements.AddRange(blockStatement.Statements.Select(ProcessStatement));
            return processBlockStatement;
        }

        private NRefactory.WhileStatement ProcessWhileStatement(WhileStatement whileStatement)
        {
            return new NRefactory.WhileStatement(ProcessCondition(whileStatement.Condition), ProcessStatement(whileStatement.Body));
        }

        private string ArgumentNameFor(string argumentName)
        {
            return CleanIllegalCharacters(argumentName);
        }

        private static string MethodNameFor(string ruleName)
        {
            return CleanIllegalCharacters(ruleName);
        }

        private static string MethodNameFor(RuleDeclarationStatement ruleDeclarationStatement)
        {
            return MethodNameFor(ruleDeclarationStatement.Name);
        }

        static string CleanIllegalCharacters(string input)
        {
	        return input.Replace(".", "_").Replace("+", "Plus");
        }
    
        NRefactory.Expression ProcessCondition(Expression condition)
        {
	        var processExpression = ProcessExpression(condition);
	        if (condition is BinaryOperatorExpression)
		        return processExpression;

	        return new NRefactory.InvocationExpression(new NRefactory.MemberReferenceExpression(processExpression,"AsBool"));
        }

	    string CSharpMethodForConditionOperator(Operator @operator)
        {
            switch (@operator)
            {
                case Operator.Assignment:
                    return "JamEquals";
                case Operator.In:
                    return "IsIn";
				case Operator.And:
		            return "And";
				case Operator.Or:
		            return "Or";
				case Operator.NotEqual:
		            return "NotJamEquals";
                default:
                    throw new NotSupportedException("Unknown conditional operator: "+@operator);
            }
        }

        public NRefactory.Expression ProcessExpressionList(NodeList<Expression> expressionList, bool mightModify = false)
        {
			var expressionsForJamListConstruction = ExpressionsForJamListConstruction(expressionList).ToArray();

			if (expressionList.Length == 1 && mightModify)
	        {
		        if (expressionList[0] is VariableDereferenceExpression)
					return new NRefactory.InvocationExpression(new NRefactory.MemberReferenceExpression(ProcessExpression(expressionList[0]), "Clone"));
	        }
			
	        if (expressionsForJamListConstruction.Length == 1)
		        return expressionsForJamListConstruction[0];

	        return new NRefactory.ObjectCreateExpression(JamListAstType, expressionsForJamListConstruction);
        }

	    IEnumerable<NRefactory.Expression> ExpressionsForJamListConstruction(NodeList<Expression> expressionList)
	    {
		    foreach (var expression in expressionList)
		    {
			    yield return ProcessExpression(expression);
		    }
	    }

	    NRefactory.Expression ProcessExpression(Expression e)
        {
			
			var literalExpression = e as LiteralExpression;
            if (literalExpression != null)
                return new NRefactory.PrimitiveExpression(literalExpression.Value);
                        
            var expansionStyleExpression = e as ExpansionStyleExpression;
            if (expansionStyleExpression != null)
                return ProcessExpansionStyleExpression(expansionStyleExpression);

            var combineExpression = e as CombineExpression;
            if (combineExpression != null)
            {
                var combineMethod = new NRefactory.MemberReferenceExpression(new NRefactory.IdentifierExpression("JamList"), "Combine");
                return new NRefactory.InvocationExpression(combineMethod, combineExpression.Elements.Select(ProcessExpression));
            }

            var invocationExpression = e as InvocationExpression;
            if (invocationExpression != null)
            {
	            return ProcessInvocationExpression(invocationExpression);
            }

		    var binaryOperatorExpression = e as BinaryOperatorExpression;
		    if (binaryOperatorExpression != null)
		    {
			    var left = ProcessExpression(binaryOperatorExpression.Left);
			    var right = ProcessExpressionList(binaryOperatorExpression.Right);

				return new NRefactory.InvocationExpression(new NRefactory.MemberReferenceExpression(left, CSharpMethodForConditionOperator(binaryOperatorExpression.Operator)), right);
		    }

		    var notOperatorExpression = e as NotOperatorExpression;
		    if (notOperatorExpression != null)
		    {
			    return new NRefactory.UnaryOperatorExpression(NRefactory.UnaryOperatorType.Not, ProcessExpression(notOperatorExpression.Expression));
		    }

            if (e == null)
                return new NRefactory.ObjectCreateExpression(JamListAstType);

            throw new NotImplementedException("CSharpFor cannot deal with " + e);
        }

	    private NRefactory.Expression ProcessInvocationExpression(InvocationExpression invocationExpression)
	    {
		    var ruleExpression = invocationExpression.RuleExpression;

		    var literalExpression = ruleExpression as LiteralExpression;
		    if (literalExpression != null)
		    {
			    var methodName = MethodNameFor(invocationExpression.RuleExpression.As<LiteralExpression>().Value);

			    return new NRefactory.InvocationExpression(new NRefactory.IdentifierExpression(methodName),
				    invocationExpression.Arguments.Select(a => ProcessExpressionList(a)));
		    }

		    var arguments = invocationExpression.Arguments.Select(a => ProcessExpressionList(a)).Prepend(ProcessExpression(ruleExpression));
			return new NRefactory.InvocationExpression(new NRefactory.MemberReferenceExpression(new NRefactory.IdentifierExpression("_dynamicRuleInvocationService"),"InvokeRule"),arguments);
		}

	    private NRefactory.Expression ProcessExpansionStyleExpression(ExpansionStyleExpression expansionStyleExpression)
        {
			//righthandside:
			//mads         ->   "mads"
			//$(mads);     ->   Globals.mads
			//$(mads_arg); ->   mads_arg
			//$($(mads));  ->   Globals.DereferenceElements(Globals.mads)
			//$($($(mads)));  ->   Globals.DereferenceElements(Globals.DereferenceElements(Globals.mads))
			//
			//@(mads)      ->   new JamList("mads")
			//@(mads:S=.exe) -> new JamList("mads").WithSuffix(".exe");

			var resultExpression = ProcessExpansionStyleExpressionVariablePreModifiers(expansionStyleExpression);

	        if (expansionStyleExpression.IndexerExpression != null)
            {
                var memberReferenceExpression = new NRefactory.MemberReferenceExpression(resultExpression, "IndexedBy");
                var indexerExpression = ProcessExpression(expansionStyleExpression.IndexerExpression);
                resultExpression = new NRefactory.InvocationExpression(memberReferenceExpression, indexerExpression);
            }

            foreach (var modifier in expansionStyleExpression.Modifiers)
            {
                var csharpMethod = CSharpMethodForModifier(modifier);

                var memberReferenceExpression = new NRefactory.MemberReferenceExpression(resultExpression, csharpMethod);
                var valueExpression = ProcessExpression(modifier.Value);
                resultExpression = new NRefactory.InvocationExpression(memberReferenceExpression, valueExpression);
            }
            return resultExpression;
        }

	    private NRefactory.Expression ProcessExpansionStyleExpressionVariablePreModifiers(ExpansionStyleExpression expansionStyleExpression)
	    {
			if (expansionStyleExpression is LiteralExpansionExpression)
				return new NRefactory.ObjectCreateExpression(JamListAstType, ProcessExpression(expansionStyleExpression.VariableExpression));
			
			//we know we are a variabledereferenceexpression now
		    expansionStyleExpression.As<VariableDereferenceExpression>();

			var literalExpression = expansionStyleExpression.VariableExpression as LiteralExpression;
		    if (literalExpression != null)
			    return ProcessIdentifier(literalExpression, literalExpression.Value);

		    return new NRefactory.InvocationExpression(
					    new NRefactory.MemberReferenceExpression(new NRefactory.IdentifierExpression("Globals"), "DereferenceElements"),
					    ProcessExpression(expansionStyleExpression.VariableExpression)
						);
	    }

	    private string CSharpMethodForModifier(VariableDereferenceModifier modifier)
        {
            switch (modifier.Command)
            {
                case 'S':
                    return "WithSuffix";
                case 'E':
                    return "IfEmptyUse";
                case 'G':
                    return "GristWith";
                case 'J':
                    return "JoinWithValue";
				case 'X':
		            return "Exclude";
				case 'I':
		            return "Include";
				case 'R':
		            return "Rooted_TODO";
				case 'P':
		            return "PModifier_TODO";
				
                default:
                    throw new NotSupportedException("Unkown variable expansion command: " + modifier.Command);
            }
        }
    }
}
