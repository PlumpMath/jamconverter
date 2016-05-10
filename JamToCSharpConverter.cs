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
		private NodeList<Statement> _topLevel;
		private IEnumerable<ActionsDeclarationStatement> _actions;
		private IEnumerable<RuleDeclarationStatement> _rules;

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
			_topLevel = new NodeList<Statement> ();
			while (true)
			{
				var statement = parser.ParseStatement();
				if (statement == null)
					break;

				_topLevel.Add (statement);
			}
			_actions = _topLevel.GetAllChildrenOfType<ActionsDeclarationStatement> ();
			_rules = _topLevel.GetAllChildrenOfType<RuleDeclarationStatement> ();

            var body = new NRefactory.BlockStatement();
			foreach (var statement in _topLevel)
            {
                var nStatement = ProcessStatement(statement);
                if (nStatement != null)
                    body.Statements.Add(nStatement);
			}
			body.Statements.Add (new NRefactory.InvocationExpression (new NRefactory.IdentifierExpression ("Globals.SendVariablesToJam")));
            
            _mainMethod = new NRefactory.MethodDeclaration { Name = "Main", ReturnType = new NRefactory.PrimitiveType("void"), Modifiers = NRefactory.Modifiers.Static, Body = body};
            _dummyType.Members.Add(_mainMethod);

			var actions = new NRefactory.TypeDeclaration {
				ClassType = NRefactory.ClassType.Class,
				Name = "Actions",
			};
			foreach (var action in _actions.DistinctBy(a => a.Name)) 
			{
				var actionWrapperBody = new NRefactory.BlockStatement();

				actionWrapperBody.Statements.Add (new NRefactory.ReturnStatement (
					new NRefactory.InvocationExpression(new NRefactory.IdentifierExpression("InvokeRule"), new NRefactory.Expression[]{
						new NRefactory.PrimitiveExpression(action.Name),
						new NRefactory.IdentifierExpression("values"),
					})
				));
				var actionMethod = new NRefactory.MethodDeclaration { Name = action.Name, ReturnType = JamListAstType, Modifiers = NRefactory.Modifiers.Static | NRefactory.Modifiers.Public, Body = actionWrapperBody };
				actionMethod.Parameters.Add (new NRefactory.ParameterDeclaration( new NRefactory.PrimitiveType("JamList[]"), "values", NRefactory.ParameterModifier.Params));
				actions.Members.Add(actionMethod);
			}
			_dummyType.Members.Add(actions);

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
			
			if (statement is AssignmentStatement)
			{
				AssignmentStatement assignmentStatement = (AssignmentStatement)statement;
				return ProcessAssignment(assignmentStatement.Left, assignmentStatement.Operator, assignmentStatement.Right);
			}

	        if (statement is OnStatement)
		        return ProcessOnStatement((OnStatement) statement);

	        return ProcessExpressionStatement((ExpressionStatement) statement);
        }

		private NRefactory.Expression GetActionModifiers (ActionsDeclarationStatement statement)
		{
			if (!statement.Modifiers.Any())
				return new NRefactory.IdentifierExpression("Jam.ActionsFlags.None");

			NRefactory.Expression result = null;

			foreach (LiteralExpression modifier in statement.Modifiers)
			{
				foreach (var name in Enum.GetNames(typeof(Jam.ActionsFlags))) {
					if (modifier.Value.ToLower() == name.ToLower()){
						var modifierExpression = new NRefactory.IdentifierExpression("Jam.ActionsFlags."+name);
						if (result == null)
							result = modifierExpression;
						else 
							result = new NRefactory.BinaryOperatorExpression(result, NRefactory.BinaryOperatorType.BitwiseAnd, modifierExpression);
						break;
					}
				}
			}

			return result;
		}

		private int GetModifierValue (ActionsDeclarationStatement statement, Jam.ActionsFlags flag)
		{
			for (int i = 0; i < statement.Modifiers.Count (); i++) 
			{
				var modifier = statement.Modifiers [i] as LiteralExpression;
				if (modifier.Value.ToLower () == flag.ToString ().ToLower ())
				{
					modifier = statement.Modifiers [i + 1] as LiteralExpression;
					return System.Int32.Parse (modifier.Value);
				}
			}
			return 0;
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
			return new NRefactory.InvocationExpression(new NRefactory.IdentifierExpression("MakeActions"), new NRefactory.Expression[]{
				new NRefactory.PrimitiveExpression(statement.Name),
				new NRefactory.PrimitiveExpression(String.Join("\n", statement.Actions)),
				GetActionModifiers(statement),
				new NRefactory.PrimitiveExpression(GetModifierValue(statement, Jam.ActionsFlags.MaxTargets)),
				new NRefactory.PrimitiveExpression(GetModifierValue(statement, Jam.ActionsFlags.MaxLine))
			});
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
			    return new NRefactory.InvocationExpression(new NRefactory.MemberReferenceExpression(new NRefactory.IdentifierExpression("Globals"), "DereferenceElementsNonFlat"), ProcessExpressionForLeftHandOfAssignment(deref.VariableExpression));

		    var variableOnTargetExpression = left as VariableOnTargetExpression;
		    if (variableOnTargetExpression != null)
		    {
			    var variableExpression = ProcessExpression(variableOnTargetExpression.Variable);
			    var targetExpression = ProcessExpressionList(variableOnTargetExpression.Targets);
				return new NRefactory.InvocationExpression(new NRefactory.MemberReferenceExpression(new NRefactory.IdentifierExpression("Globals"), "GetOrCreateVariableOnTargetContext"), targetExpression, variableExpression);
			}

			throw new NotImplementedException();
	    }

	    private NRefactory.Expression ProcessIdentifier(Expression parentExpression, string identifierName)
	    {
		    var cleanName = CleanIllegalCharacters(identifierName);

		    if (parentExpression != null)
		    {
				var parentRule = FindParentOfType<RuleDeclarationStatement>(parentExpression);

				if (parentRule != null) 
				{
					var implicitIndex = GetImplicitVariableIndex (identifierName);
					if (implicitIndex != 0) {
						if (parentRule.Arguments.Length >= implicitIndex)
							return new NRefactory.IdentifierExpression (parentRule.Arguments [implicitIndex - 1]);
						else
							return new NRefactory.IdentifierExpression (GetImplicitVariableName (implicitIndex));
					}
					if (parentRule.Arguments.Contains (identifierName))
						return new NRefactory.IdentifierExpression (cleanName);
				}

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

		static int GetImplicitVariableIndex(string variable)
		{
			if (variable.Length == 1) 
			{
				var ch = variable [0];
				if (ch >= '1' && ch <= '9')
					return ch - '0';
				else if (ch == '<')
					return 1;
				else if (ch == '>')
					return 2;
			}
			return 0;
		}

		static string GetImplicitVariableName(int index)
		{
			return "implicitArgument" + index;
		}

		static string[] SetupArgumentsFor(RuleDeclarationStatement ruleDeclaration)
		{
			//because the parser always interpets an invocation without any arguments as one with a single argument: an empty expressionlist,  let's make sure we always are ready to take a single argument
			var arguments = ruleDeclaration.Arguments;

			var variables = ruleDeclaration.GetAllChildrenOfType<VariableDereferenceExpression> ();

			foreach (var v in variables) 
			{
				var variable = v.VariableExpression.As<LiteralExpression>();
				var implicitIndex = GetImplicitVariableIndex (variable.Value);
				if (implicitIndex != 0) 
				{
					if (arguments.Count () < implicitIndex) 
					{
						var newArguments = new string[implicitIndex];
						Array.Copy (arguments, newArguments, arguments.Length);
						for (int i = 0; i < implicitIndex; i++) 
						{
							if (newArguments[i] == null)
								newArguments[i] = GetImplicitVariableName (i + 1);
						}
						arguments = newArguments;
					}
				}
			}

			if (arguments.Length == 0)
				arguments = new[] { "dummyArgument" };

			return arguments;
		}

        private void ProcessRuleDeclarationStatement(RuleDeclarationStatement ruleDeclaration)
        {
			var arguments = SetupArgumentsFor (ruleDeclaration);

            var body = new NRefactory.BlockStatement();

			var methodName = MethodNameFor(ruleDeclaration);

            var processRuleDeclarationStatement = new NRefactory.MethodDeclaration()
            {
				Name = methodName,
                ReturnType = JamListAstType,
                Modifiers = NRefactory.Modifiers.Static,
                Body = body
            };
            processRuleDeclarationStatement.Parameters.AddRange(arguments.Select(a => new NRefactory.ParameterDeclaration(JamListAstType, ArgumentNameFor(a))));

			if (IsActions(methodName))
				body.Statements.Add (new NRefactory.InvocationExpression(new NRefactory.IdentifierExpression(ActionsNameFor(methodName)), arguments.Select(a => new NRefactory.IdentifierExpression(ArgumentNameFor(a)))));

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

		private bool IsActions(string name)
		{
			return _actions.Any(x => x.Name == name);
		}

		private bool IsRule(string name)
		{
			return _rules.Any(x => x.Name == name);
		}
			
        private static string MethodNameFor(string ruleName)
        {
            return CleanIllegalCharacters(ruleName);
        }

		private static string ActionsNameFor(string name)
		{
			return "Actions."+name;
		}

        private static string MethodNameFor(RuleDeclarationStatement ruleDeclarationStatement)
        {
            return MethodNameFor(ruleDeclarationStatement.Name);
        }

        static string CleanIllegalCharacters(string input)
        {
	        return input.Replace(".", "_").Replace("+", "Plus");
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
				var name = invocationExpression.RuleExpression.As<LiteralExpression>().Value;
                var methodName = MethodNameFor(name);
				if (IsActions (name) && !IsRule(name))
					methodName = ActionsNameFor(methodName);

                return new NRefactory.InvocationExpression(new NRefactory.IdentifierExpression(methodName), invocationExpression.Arguments.Select(a=>ProcessExpressionList(a)));
            }

            if (e == null)
                return new NRefactory.ObjectCreateExpression(JamListAstType);

            throw new ParsingException("CSharpFor cannot deal with " + e);
        }

        private NRefactory.Expression ProcessVariableDereferenceExpression(VariableDereferenceExpression dereferenceExpression)
        {
			//righthandside:
			//mads         ->   "mads"
			//$(mads);     ->   Globals.mads
			//$(mads_arg); ->   mads_arg
			//$($(mads));  ->   Globals.DereferenceElements(Globals.mads)
			//$($($(mads)));  ->   Globals.DereferenceElements(Globals.DereferenceElements(Globals.mads))

			NRefactory.Expression resultExpression = null;

			var literalExpression = dereferenceExpression.VariableExpression as LiteralExpression;
	        if (literalExpression != null)
	        {
		        resultExpression = ProcessIdentifier(literalExpression, literalExpression.Value);
	        }

	        var nestedDeref = dereferenceExpression.VariableExpression as VariableDereferenceExpression;
	        if (nestedDeref != null)
	        {
		        resultExpression = new NRefactory.InvocationExpression(new NRefactory.MemberReferenceExpression(new NRefactory.IdentifierExpression("Globals"), "DereferenceElements"), ProcessVariableDereferenceExpression(nestedDeref));
	        }

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
