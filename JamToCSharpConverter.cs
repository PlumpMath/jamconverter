﻿using System;
using System.Collections.Generic;
using System.Linq;
using jamconverter.AST;
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
            _syntaxTree.Members.Add(new NRefactory.UsingDeclaration("static BuiltinFunctions"));
            _dummyType = new NRefactory.TypeDeclaration {Name = "Dummy"};
            _syntaxTree.Members.Add(_dummyType);

            _staticGlobals = new NRefactory.TypeDeclaration {Name = "StaticGlobals", BaseTypes = { new NRefactory.SimpleType("GlobalVariables") }};
            _syntaxTree.Members.Add(_staticGlobals);

             _dummyType.Members.Add(new NRefactory.FieldDeclaration() {ReturnType = StaticGlobalsType, Modifiers = NRefactory.Modifiers.Static, Variables = {new NRefactory.VariableInitializer("Globals", new NRefactory.ObjectCreateExpression(StaticGlobalsType))}});

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

            return ProcessExpressionStatement((ExpressionStatement) statement);
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
                InExpression = ProcessExpressionList(statement.List),
                EmbeddedStatement = ProcessStatement(statement.Body)
            };
        }

        private NRefactory.ExpressionStatement ProcessExpressionStatement(ExpressionStatement expressionStatement)
        {
            if (expressionStatement.Expression is InvocationExpression)
                return new NRefactory.ExpressionStatement(ProcessExpression(expressionStatement.Expression));

            if (expressionStatement.Expression is BinaryOperatorExpression)
                return new NRefactory.ExpressionStatement(ProcessAssignmentExpressionStatement((BinaryOperatorExpression) expressionStatement.Expression));

            throw new ArgumentException("Unsupported node: " + expressionStatement.Expression);
        }

        private NRefactory.Expression ProcessAssignmentExpressionStatement(BinaryOperatorExpression assignmentExpression)
        {
	        return ProcessAssignment(assignmentExpression.Left, assignmentExpression.Operator, assignmentExpression.Right);
        }

	    private NRefactory.Expression ProcessAssignment(Expression left, Operator @operator, NodeList<Expression> right)
	    {
		    var leftExpression = VariableExpressionFor(left);

		    switch (@operator)
		    {
			    case Operator.Assignment:

				    return new NRefactory.AssignmentExpression(leftExpression, NRefactory.AssignmentOperatorType.Assign,
					    ProcessExpressionList(right, mightModify:true));

			    default:
				    var csharpMethodNameForAssignmentOperator = CsharpMethodNameForAssignmentOperator(@operator);
				    var memberReferenceExpression = new NRefactory.MemberReferenceExpression(leftExpression,
					    csharpMethodNameForAssignmentOperator);
				    var processExpression = ExpressionsForJamListConstruction(right);
				    return new NRefactory.InvocationExpression(memberReferenceExpression, processExpression);
		    }
	    }

	    private NRefactory.Expression VariableExpressionFor(Expression expression)
        {
            var literalExpression = expression as LiteralExpression;
            if (literalExpression != null)
            {
                var variableName = literalExpression.Value;
                var cleanName = CleanIllegalCharacters(variableName);

                var parentRule = FindParentOfType<RuleDeclarationStatement>(expression);
                if (parentRule != null && parentRule.Arguments.Contains(variableName))
                    return new NRefactory.IdentifierExpression(cleanName);

                var forLoop = FindParentOfType<ForStatement>(expression);
                if (forLoop != null && forLoop.LoopVariable.Value == variableName)
                    return new NRefactory.IdentifierExpression(cleanName);

                return StaticGlobalVariableFor(cleanName);
            }

            var dereferenceExpression = expression as VariableDereferenceExpression;
            if (dereferenceExpression != null)
                return new NRefactory.IndexerExpression(new NRefactory.IdentifierExpression("Globals"), ProcessExpression(expression));

		    var variableOnTargetExpression = expression as VariableOnTargetExpression;
			if (variableOnTargetExpression != null)
				return new NRefactory.IdentifierExpression("VariableOnTargetTODO");

            throw new ParsingException();
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

        private static string CsharpMethodNameForAssignmentOperator(Operator assignmentOperator)
        {
            switch (assignmentOperator)
            {
                case Operator.Append:
                    return "Append";
                case Operator.Subtract:
                    return "Subtract";
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

            if (!(ruleDeclaration.Body.Statements.Last() is ReturnStatement))
                body.Statements.Add(new NRefactory.ReturnStatement(new NRefactory.NullReferenceExpression()));
            
            _dummyType.Members.Add(processRuleDeclarationStatement);
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
            return input.Replace(".", "_");
        }
    
        NRefactory.Expression ProcessCondition(Condition condition)
        {
            var conditionWithoutNegation = ConditionWithoutNegation(condition);

            return condition.Negated ? new NRefactory.UnaryOperatorExpression(NRefactory.UnaryOperatorType.Not, conditionWithoutNegation) : conditionWithoutNegation;
        }

        private NRefactory.Expression ConditionWithoutNegation(Condition condition)
        {
            if (condition.Right == null)
                return new NRefactory.InvocationExpression(new NRefactory.MemberReferenceExpression(ProcessExpression(condition.Left), "AsBool"));

            return new NRefactory.InvocationExpression(new NRefactory.MemberReferenceExpression(ProcessExpression(condition.Left), CSharpMethodForConditionOperator(condition.Operator)), ExpressionsForJamListConstruction(condition.Right));
        }

        string CSharpMethodForConditionOperator(Operator @operator)
        {
            switch (@operator)
            {
                case Operator.Assignment:
                    return "JamEquals";
                case Operator.In:
                    return "IsIn";
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
                        
            var dereferenceExpression = e as VariableDereferenceExpression;
            if (dereferenceExpression != null)
                return ProcessVariableDereferenceExpression(dereferenceExpression);

            var combineExpression = e as CombineExpression;
            if (combineExpression != null)
            {
                var combineMethod = new NRefactory.MemberReferenceExpression(new NRefactory.IdentifierExpression("JamList"), "Combine");
                return new NRefactory.InvocationExpression(combineMethod, combineExpression.Elements.Select(ProcessExpression));
            }

            var invocationExpression = e as InvocationExpression;
            if (invocationExpression != null)
            {
                var methodName = MethodNameFor(invocationExpression.RuleExpression.As<LiteralExpression>().Value);

                return new NRefactory.InvocationExpression(new NRefactory.IdentifierExpression(methodName), invocationExpression.Arguments.Select(a=>ProcessExpressionList(a)));
            }

            if (e == null)
                return new NRefactory.ObjectCreateExpression(JamListAstType);

            throw new ParsingException("CSharpFor cannot deal with " + e);
        }

        private NRefactory.Expression ProcessVariableDereferenceExpression(VariableDereferenceExpression dereferenceExpression)
        {
            var variableExpression = VariableExpressionFor(dereferenceExpression.VariableExpression);

            NRefactory.Expression resultExpression = variableExpression;

            if (dereferenceExpression.IndexerExpression != null)
            {
                var memberReferenceExpression = new NRefactory.MemberReferenceExpression(resultExpression, "IndexedBy");
                var indexerExpression = ProcessExpression(dereferenceExpression.IndexerExpression);
                resultExpression = new NRefactory.InvocationExpression(memberReferenceExpression, indexerExpression);
            }

            foreach (var modifier in dereferenceExpression.Modifiers)
            {
                var csharpMethod = CSharpMethodForModifier(modifier);

                var memberReferenceExpression = new NRefactory.MemberReferenceExpression(resultExpression, csharpMethod);
                var valueExpression = ProcessExpression(modifier.Value);
                resultExpression = new NRefactory.InvocationExpression(memberReferenceExpression, valueExpression);
            }
            return resultExpression;
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
                default:
                    throw new NotSupportedException("Unkown variable expansion command: " + modifier.Command);
            }
        }
    }
}
