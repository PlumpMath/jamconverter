﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using NiceIO;
using NUnit.Framework;

namespace jamconverter.Tests
{
    [TestFixture]
    class ComparisonTests
    {
        [Test]
        public void Simple()
        {
            AssertConvertedProgramHasIdenticalOutput("Echo Hello ;");
        }
        
        [Test]
        public void TwoEchos()
        {
            AssertConvertedProgramHasIdenticalOutput("Echo Hello ; Echo There ;");
        }

        [Test]
        public void EchoMultipleLiterals()
        {
            AssertConvertedProgramHasIdenticalOutput("Echo Hello There Sailor ;");
        }

        [Test]
        public void VariableExpansion()
        {
            AssertConvertedProgramHasIdenticalOutput(
@"
myvar = 123 ;
Echo $(myvar) ;

foo = FOO ;
for x in $(foo) { Echo $(x) ; }
for x in ""$(foo)"" { Echo $(x) ; }
for x in $(""foo)"" { Echo $(x) ; }
#for x in $\(foo) { Echo $(x) ; }
for x in \$(foo) { Echo $(x) ; }
"
			);
        }


		[Test]
		public void DModifier()
		{
			AssertConvertedProgramHasIdenticalOutput(
@"
myvar = some/dir/myfile.cs ;
Echo $(myvar:D) ;
myvar = file.cs ;
Echo $(myvar:D) ;
myfar = file1.cs harry/sally/subdir/yeah.cpp ;
Echo $(myvar:D) ;

a = c:/unity/build/temp/Unity.CommonTools.dll ;
Echo $(a:D) ;
"
            
			);
		}

        [Test]
        public void Emptyness()
        {
            AssertConvertedProgramHasIdenticalOutput(
@"
empty = ;
if $(empty) { Echo yes ; } else { Echo no ; }
if $(empty) = """" { Echo yes ; } else { Echo no ; }

rule ReturnNothing { }
empty = [ ReturnNothing ] ;
if $(empty) { Echo yes ; } else { Echo no ; }
if $(empty) = """" { Echo yes ; } else { Echo no ; }

empty = """" ;
if $(empty) { Echo yes ; } else { Echo no ; }
if $(empty) = """" { Echo yes ; } else { Echo no ; }
"

            );
        }


        [Test]
        [Ignore("too hard")]
        public void ImplicitReturnValues()
        {
            AssertConvertedProgramHasIdenticalOutput(
@"

rule OtherRule
{
  return a b c ;
}

rule LooksLikeIDoNotReturnAnything
{
   OtherRule ;
}

rule LooksLikeIDoNotReturnAnything2
{
   a = 3 ;
}

rule LooksLikeIDoNotReturnAnything3
{
    if b = c { a = 1 ; } else { a = 2 ; }
}

rule LooksLikeIDoNotReturnAnything3
{
    if b = b { a = 1 ; } 
    
}

Echo [ LooksLikeIDoNotReturnAnything ] ;
Echo [ LooksLikeIDoNotReturnAnything2 ] ;
Echo [ LooksLikeIDoNotReturnAnything3 ] ;
Echo close ;

"

            );
        }


        [Test]
        public void SlashingModifier()
        {
            AssertConvertedProgramHasIdenticalOutput(
@"
myvar = so\me/dir/myf\ile.cs ;
Echo $(myvar:\\) ;
Echo $(myvar:/) ;
"
            );
        }

        [Test]
		public void DereferenceCombineExpression()
		{
			AssertConvertedProgramHasIdenticalOutput(
@"
abc = 123 ; 
myvar = a ; 
Echo $($(myvar)bc:G=hi) ;

Echo ""foo  $(abc)bar"" ;
"
			);
		}

		[Test]
		public void ValueSemantics()
		{
			AssertConvertedProgramHasIdenticalOutput(
@"myvar = a b c ;
myvar2 = $(myvar) ;
myvar2 += d ; 
Echo $(myvar) ;
Echo $(myvar2) ;

rule MyFunc myarg
{
  myarg += a ;
}

myvar3 = hello ;
MyFunc $(myvar3) ;
Echo $(myvar3) ;

#test return value value semantics

myreturnvalue = a b c ;

rule ReturnMe
{
  return $(myreturnvalue) ;
}

myvar5 = [ ReturnMe ] ;
myvar5 += d ;
Echo $(myvar5) ;
Echo $(myreturnvalue) ;




");
		}

        [Test]
        public void CompareWithString()
        {
            AssertConvertedProgramHasIdenticalOutput(
@"
myvar = harry ;
if $(myvar) = harry { Echo yes ; } else { Echo no ; }
myvar = ;
if $(myvar) = """" { Echo yes ; } else { Echo no ; }

");
        }

        [Test]
        public void DoubleVariableAssignment()
        {
            AssertConvertedProgramHasIdenticalOutput("myvar = 123 ; myvar = 234 ; Echo $(myvar) ;");
        }

        [Test]
        public void IfStatement()
        {
            AssertConvertedProgramHasIdenticalOutput(
@"
myvar = 123 ; 
if $(myvar) { Echo msg1 ; } else { Echo msg1a ; }

if ! $(myvar) { Echo msg2 ; } else { Echo msg2a ; }

if $(myvar) = 123 { Echo msg3 ; }  else { Echo msg3a ; }

if $(myvar) = 321 { Echo msg5 ; } else { Echo msg5a ; }
#This doesnt work because jam is crazy: if ! $(myvar) = 321 { Echo msg5 ; } else { Echo msg5a ; }

myemptyvar = ;
if $(myemptyvar) { Echo msgA ; } else { Echo msg6a ; }
if ! $(myemptyvar) { Echo msgB ; } else { Echo msg7a ; }

if $(myvar) = 3212 { Echo yes ; } else if $(myvar) = 123 { Echo no ; } else Echo Boink ;
myvar = neither ;
if $(myvar) = 3212 { Echo yes ; } else if $(myvar) = 123 { Echo no ; } else Echo Boink ;

        Echo end ;
");
        }

        [Test]
        public void AndOperator()
        {
            AssertConvertedProgramHasIdenticalOutput(
@"

type = Cpp ;
buildMode = BUILDMODE_DYNAMICLIB ;
if $(type) = Cpp && $(buildMode) = BUILDMODE_DYNAMICLIB { Echo yes ; } else { Echo no ; }

buildMode = bla ;
if $(type) = Cpp && $(buildMode) = BUILDMODE_DYNAMICLIB { Echo yes ; } else { Echo no ; }


if $(type) in Cs Cpp Exe && $(type) = Cpp { Echo Yes ; } else { Echo no ; }

");
        }

        [Test]
        public void EqualsConditional()
        {
            AssertConvertedProgramHasIdenticalOutput(
@"
myvar = 123 ; 
if $(myvar) = 123 { Echo Yes1 ; } 
if $(myvar) = 321 { Echo Yes2 ; } 
");
        }

        [Test]
        public void InOperator()
        {
            AssertConvertedProgramHasIdenticalOutput(
@"
myvar = 123 ; 
if $(myvar) in 123 { Echo Yes ; } else { Echo No ; } 
if $(myvar) in a b 123 { Echo Yes ; } else { Echo No ; } 
if $(myvar) in a b 125 { Echo Yes ; } else { Echo No ; } 

myvar = a b ;
if $(myvar) in a b { Echo Yes ; } else { Echo No ; }
if $(myvar) in a x b { Echo Yes ; } else { Echo No ; }
if $(myvar) in a c { Echo Yes ; } else { Echo No ; }
if $(myvar) in b c { Echo Yes ; } else { Echo No ; }
if $(myvar) in d e { Echo Yes ; } else { Echo No ; }


");
        }


        [Test]
        public void Assignments()
        {
            AssertConvertedProgramHasIdenticalOutput(
@"myvar = a b ; 
#Echo $(myvar) ; 

Echo check1 ;
harry ?= sally ;
Echo $(harry) ;
Echo check2 ;


harry ?= sailor ;
Echo $(harry) ;

myvar1 = a ;
myvar2 = a b ;

myvars = myvar1 myvar2 ;
$(myvars) += c ;
Echo $(myvar1) ;
Echo $(myvar2) ;
Echo $(myvars) ;

rule MyRule myarg
{
   myarg = 2 ;
   Echo myarg $(myarg) ;
}
myarg = 5 ;
MyRule 4 ;
Echo $(myarg) ;

myvar = harry johny ;
$(myvar)_sally = 123 ;
Echo $(harry_sally) _ $(johny_sally) ;

");
        }

		[Test]
	    public void DynamicRuleInvocation()
	    {

		    AssertConvertedProgramHasIdenticalOutput(
				@"
rule harry arg0 { Echo harry $(arg0) ; return harryreturn ; }
rule sally arg0 { Echo sally $(arg0) ; return sallyreturn ; }
rule MakeArg2 { Echo makearg2 ; return one ; }

myrules = harry sally ;
whynot = yolo ;
#Echo [ $(myrules) $(whynot) [ MakeArg2 ] ] ;
$(myrules) $(whynot) [ MakeArg2 ] ;
");
	    }


	    [Test]
        public void CombineExpression()
        {
            AssertConvertedProgramHasIdenticalOutput(
@"myvar = john doe ; 
Echo $(myvar)postfix ; 
Echo $(myvar).* ;
Echo *.$(myvar) ;

myemptyvar = ;
Echo $(myvar)$(myemptyvar)hello ;

Echo one$(myvar)two ;
Echo one$(myvar)$(myvar)two ;



");
        }

        [Test]
        public void CustomRule()
        {
            AssertConvertedProgramHasIdenticalOutput(
                @"rule customrule { Echo Hello ; } customrule ;"
                );
        }

        [Test]
        public void CustomRuleWithArgument()
        {
            AssertConvertedProgramHasIdenticalOutput(
                @"rule customrule arg1 { Echo $(arg1) ; } customrule hello ;"
                );
        }

        [Test]
        public void SuffixVariableExpansion()
        {
            AssertConvertedProgramHasIdenticalOutput(
@"
myvar = main.cs ; 
Echo $(myvar:S=.cpp) ;

myvar = main.cs.pieter ; 
Echo $(myvar:S=.cpp:S=.exe) ;

myvar = main.cs.pieter ; 
Echo $(myvar:S=) ;

myvar = hello sailor ;
Echo $(myvar:S) ;
myvar = hello.bat ;
Echo $(myvar:S) ;
myvar = hello. ;
Echo $(myvar:S) ;
");
        }

		[Test]
		[Ignore("Need to investigate jam behaviour")]
		public void MultipleDifferentModifiers()
		{
			AssertConvertedProgramHasIdenticalOutput(
@"
mylist = hello there sailor ;
Echo $(mylist:I=hello:S=exe:I=sailor:X=hello) ;
");
		}


		[Test]
        public void EmptyVariableExpansion()
        {
            AssertConvertedProgramHasIdenticalOutput(
@"
myvar = ; 
Echo $(myvar:E=alternative) ;

myvar = realvalue ;
Echo $(myvar:E=alternative) ;

myvar = ;
Echo $(myvar:E=*) ;

myvar = ;
Echo $(myvar:E=) ;

");
        }

        [Test]
        [Ignore("wontfix")]
        public void ExpressionList()
        {
            AssertConvertedProgramHasIdenticalOutput(
@"

rule ReturnMe args
{
   Echo hello from $(args) ;
   return $(args) ;
}

local mylist = 
  abc
  [ ReturnMe harry sally ]
  [ ReturnMe OhNo ]
  dog
  [ ReturnMe Last ]
;

Echo $(mylist) ;

");
        }

        [Test]
        public void JoinValueExpansion()
        {
            AssertConvertedProgramHasIdenticalOutput(
@"
myvar = im on a boat ; 
Echo $(myvar:J=_) ;
Echo $(myvar:J=) ;
Echo $(myvar:J) ;
");
        }

        [Test]
		[Ignore(("I hope we dont need this behaviour"))]
		public void KeywordsInExpressionList()
        {
            AssertConvertedProgramHasIdenticalOutput(
@"
myvar = i am on in for while if a boat ; 
Echo $(myvar) ;
");
        }


        [Test]
        public void EvaluationOrder()
        {
            AssertConvertedProgramHasIdenticalOutput(
@"

rule One
{
   Echo One ;
   return one harry ;
}

rule Two
{
   Echo Two ;
   return two sally ;
}

myvar = [ One ] [ Two ] ;
Echo $(myvar) ;

");
        }


        [Test]
	    public void GreaterThanOperator()
	    {
			AssertConvertedProgramHasIdenticalOutput(
@"
rule Return3 { return 3 ; }
rule Return0 { return 0 ; }
if [ Return3 ] > 1 { Echo Yes ; } else { Echo no ; }
if [ Return0 ] > 1 { Echo Yes ; } else { Echo no ; }

if [ Return3 ] < 3 { Echo Yes ; } else { Echo no ; }
if [ Return0 ] < 3 { Echo Yes ; } else { Echo no ; }

");
		}

        [Test]
        public void GristVariableExpansion()
        {
            AssertConvertedProgramHasIdenticalOutput(@"
myvar = harry ; 
Echo $(myvar:G=mygrist) ;

myvar = <pregisted>realvalue ;
Echo $(myvar:G=mygrist) ;

Echo $(myvar:G=<gristwithanglebrackets>) ;

");
        }

        [Test]
        public void RuleReturningValue()
        {
            AssertConvertedProgramHasIdenticalOutput(
@"
rule GimmeFive
{
  return five ;
}
Echo [ GimmeFive ] ;
");
        }

        [Test]
        public void RuleAndVariableWithDotInName()
        {
            AssertConvertedProgramHasIdenticalOutput(
                @"

rule I.Love.Dots dot.in.argument
{
  return $(dot.in.argument) dot.in.literal ;
}

dots.in.variable = 3 ;
Echo [ I.Love.Dots 18 ] ;
Echo  $(dots.in.variable) ;
");
        }

        [Test]
        public void BlockStatement()
        {
            AssertConvertedProgramHasIdenticalOutput(
                @"
{
  Echo a ; 
  {
      Echo b ;
  }
  Echo c ;
}

Echo d ;
");
        }

        [Test]
        public void BuiltinMD5()
        {
            AssertConvertedProgramHasIdenticalOutput("Echo [ MD5 harry ] ;");
        }

	    [Test]
	    public void EmptyRuleInvocation()
	    {
			AssertConvertedProgramHasIdenticalOutput("rule Hello { } Hello ; Echo test ;");
		}

		[Test]
		public void AssignResultOfRuleInvocation()
		{
			AssertConvertedProgramHasIdenticalOutput(
@"
rule MyRule arg0 : arg1 { Echo $(arg0) $(arg1) ; return ""Hello"" ; }

myvar = [ MyRule a : b ] ;
Echo $(myvar) ;
"
			);
		}

		[Test]
		public void RuleInvocationWithImplicitParameters()
		{
			AssertConvertedProgramHasIdenticalOutput(@"
				# Single implicit parameter
				rule Hello1 { Echo $(1) ; } Hello1 a ;
				# Two implicit parameter with only one being referenced
				rule Hello2 { Echo $(2) ; } Hello2 a : b ;
				# argument with explicit name being referenced using numeric reference
				rule Hello3 explicitA : explicitB { Echo $(2) _ $(explicitB) ; } Hello3 a : b ;
				# > < syntax
				rule Hello4 { Echo $(<) _ $(>) ; } Hello4 a : b ;
			"
			);
		}
						
		[Test]
        public void VariableDereferencingWithIndexer()
        {
            AssertConvertedProgramHasIdenticalOutput(
@"
myvar = a b c d e ; 
Echo $(myvar[2]) ;
Echo $(myvar[2-3]) ;
Echo $(myvar[2-]) ;

myindex = 3 ;
Echo $(myvar[$(myindex)]) ;

myindices = 3 4 1 ;
Echo $(myvar[$(myindices)]) ;

Echo $(myvar[$(myindices)]:S=.mysuffix) ;

#index out of range:
myvar = a b c ;
Echo $(myvar[4]) ;
Echo $(myvar[0]) ;
Echo $(myvar[0-4]) ;


myvar = a b c ;
myindices = 1 5 3 ;  #note 5 is out of range
Echo $(myvar[$(myindices)]) ;

");
        }

		[Test]
		public void Braces()
		{
			AssertConvertedProgramHasIdenticalOutput(
@"
if x { }
# if x {} # Syntax error in Jam.
Echo end of test ;
"
			);
		}

        [Test]
        public void AppendOperator()
        {
            AssertConvertedProgramHasIdenticalOutput(
@"
myvar = a ;
myvar += b c ;
Echo $(myvar) ;
");
        }

        [Test]
        public void While()
        {
            AssertConvertedProgramHasIdenticalOutput(
@"
myvar = one two three four ;
while $(myvar)
{
   Echo $(myvar) ;
   myvar -= $(myvar[1]) ;
}");
        }

	    [Test]
	    public void Conditions()
	    {
			AssertConvertedProgramHasIdenticalOutput(
@"
one = 1 ;

if $(one) && $(zero) { Echo Yes ; } else { Echo no ; }
if $(one) && $(one) { Echo Yes ; } else { Echo no ; }
if $(zero) || $(one) { Echo Yes ; } else { Echo no ; }
if $(zero) || $(zero) { Echo Yes ; } else { Echo no ; }
if $(zero) != $(one) { Echo Yes ; } else { Echo no ; }
if $(zero) != $(zero) { Echo Yes ; } else { Echo no ; }
if $(zero) = $(one) { Echo Yes ; } else { Echo no ; }
if $(zero) = $(zero) { Echo Yes ; } else { Echo no ; }

if $(zero) { Echo with parenthesis ; }

if $(zero) && $(one) = 1 { Echo Yes ; } else { Echo no ; }

a = 1 ;
c = 1 ;
if ( ! $(a) || ! $(b) ) && $(c) { Echo Yes ; } else { Echo no ; }


");
		}

        [Test]
        public void ForLoop()
        {
            AssertConvertedProgramHasIdenticalOutput(
@"
mylist = a b c d e ;
for myvar in $(mylist) f g
{
  if $(myvar) = c { Echo continueing ; continue ; }

  if $(myvar) = f { break ; }

  Echo $(myvar) ;
}


one = 1 ;
two = 2 ;
myvars = one two ; #evil bonus points: add myvar here
for myvar in $(myvars)
{
   Echo $(myvar) $($(myvar)) ;
}

for t in $(mylist)
{
   for t2 in $(mylist)
   {
       Echo $(t2) $(t) ;
   }
}


#forloop using a already declared local:
local harry = 3 ;
for harry in $(mylist)
{
   Echo $(harry) ;
}


#forloop over an empty value
myempty = ;
for v in $(myempty)
{
  Echo ohreally ;
}

");
        }

        [Test]
        public void LocalScoping()
        {
            var jam1 =
@"


myvar = 123 ;

include file2.jam ;

Echo $(myvar) from file 1 ;
MyRule ;

";
            var jam2 =
@"

local myvar = harry ;

rule MyRule
{
   Echo $(myvar) from MyRule ;
}

MyRule ;

";

            var jamProgram = new ProgramDescripton()
            {
                new SourceFileDescription() { Contents = jam1, File = new NPath("file1.jam") },
                new SourceFileDescription() { Contents = jam2, File = new NPath("file2.jam") },
            };

            AssertConvertedProgramHasIdenticalOutput(jamProgram);
        }

        [Test]
        public void SwitchStatement()
        {
            AssertConvertedProgramHasIdenticalOutput(
@"

rule MySwitch myvar
{
   switch $(myvar)
   {
       case a :
         Echo I was a ;
       case b :
         Echo I was b ;
   }
   Echo after case ;
}

MySwitch a ;
MySwitch b ;
MySwitch c ;

");
        }

        [Test]
        public void DynamicVariables()
        {
            AssertConvertedProgramHasIdenticalOutput(
@"
mylist = a b c d e ; 
myvar = mylist ;

Echo $($(myvar)) ;
$(myvar) = 1 2 3 ;

Echo $(mylist) ;

");
        }

		[Test]
		public void UpperLowerCaseModifiers()
		{
			AssertConvertedProgramHasIdenticalOutput(
				@"
mylist = hello there sailor.c ; 
Echo $(mylist:U) ;
Echo $(mylist:L) ;
");
		}


		[Test]
		public void IncludeModifier()
		{
			AssertConvertedProgramHasIdenticalOutput(
@"
mylist = hello there sailor.c ; 
Echo $(mylist:I=th) ;

patterninvar = sai ;
Echo $(mylist:I=$(patterninvar)) ;

#make test for regex
Echo $(mylist:I=hel+) ;

# Jam treats double backslashes like one that still escapes the next character.
# Both expressions should match 'sailor.c'.
Echo $(mylist:I=\.c) ;
Echo $(mylist:I=\\.c) ;

pathWithBackslash = a\\b ;
Echo $(pathWithBackslash) ;
#Echo $(pathWithBackslash:I=\\) ; # Not valid in Jam. Jam does one level of escaping, regex another.
Echo $(pathWithBackslash:I=\\\\) ;

Echo $(mylist:I=\\.c$) ;
Echo $(mylist:I=\\.c\$) ;

filter = hello there ;
Echo $(mylist:I=$(filter)) ;


");
		}

        [Test]
        public void ExcludeModifier()
        {
            AssertConvertedProgramHasIdenticalOutput(
@"
mylist = hello there sailor.c ; 
Echo $(mylist:X=th) ;

patterninvar = sai ;
Echo $(mylist:X=$(patterninvar)) ;

#make test for regex
Echo $(mylist:X=hel+) ;

# Jam treats double backslashes like one that still escapes the next character.
# Both expressions should match 'sailor.c'.
Echo $(mylist:X=\.c) ;
Echo $(mylist:X=\\.c) ;

pathWithBackslash = a\\b ;
Echo $(pathWithBackslash) ;
#Echo $(pathWithBackslash:X=\\) ; # Not valid in Jam. Jam does one level of escaping, regex another.
Echo $(pathWithBackslash:X=\\\\) ;

Echo $(mylist:X=\\.c$) ;
Echo $(mylist:X=\\.c\$) ;

filter = hello there ;
Echo $(mylist:X=$(filter)) ;


");
        }

        [Test]
        public void BModifier()
        {
            AssertConvertedProgramHasIdenticalOutput(
@"
myfile = a/b/c/d.hello ;
Echo $(myfile:B) ;
Echo $(myfile:B=amazing) ;

Echo $(myfile:BS) ;

");
        }

        [Test]
        public void AModifier()
        {
            AssertConvertedProgramHasIdenticalOutput(
@"
local dollar = $ ;
local open = \( ;
local close = \) ;
local myvar = $(dollar)$(open)name$(close) ;
name = harry ;
Echo $(myvar:A) ;

");
        }

        [Test]
        public void WModifier()
        {
            AssertConvertedProgramHasIdenticalOutput(
@"
myvar = c:/unity/* ;
Echo $(myvar:W) ;

chop = c:/ ;
Echo $(myvar:W=$(chop)) ;

multiple = c:/ c:/unity ;
Echo $(multiple:W) ;

");
        }

        [Test]
	    public void Escaping()
	    {
		    AssertConvertedProgramHasIdenticalOutput(
@"
mylist = a\ b   a\\b    a\bb   a\n\r\t\bc  a\$b  a$b ;
for e in $(mylist) {
  Echo $(e) ;
}
"
			);
	    }

	    [Test]
		public void Quoting()
	    {
		    AssertConvertedProgramHasIdenticalOutput(
@"
mylist = foo"" ""bar a\""b ""a b c"": ;
for e in $(mylist) {
  Echo $(e) ;
}

local dollar = ""$"" ;
Echo $(dollar) ;
"
			);
	    }

	    [Test]
	    public void Regex()
	    {
			AssertConvertedProgramHasIdenticalOutput(
@"
x = x ;
mylist = x ab) ;
Echo 1 $(mylist:I=$(x)) ;
#Echo 2 $(mylist:I=$\(x\)) ;
#Echo 3 $(mylist:I=\$(x)) ;
#Echo 4 $(mylist:I=\\$(x)) ;
#Echo 5 $(mylist:I=b($$)) ;
#Echo 6 $$\(x) ;
"
			);
	    }

		[Test]
		public void Parenthesis()
		{
			AssertConvertedProgramHasIdenticalOutput(
@"
# Jam does not handle parenthesis special at all (despite what you would expect in conditional expressions).

# Lines with ## are failing to parse correctly on our parser even though they are valid Jam.

# Lines with ### are parsing correctly but we generate invalid code for them.

Echo (a  b  c) ;
Echo $(a) ;

if (a) {
  Echo a ;
}

if (((b))) {
  Echo b ;
}


isTrue = a ;
isFalse = ;

if $(isTrue) || $(isFalse) || $(isFalse) { Echo yes1 ; } else { Echo no1 ; }
if $(isTrue) || $(isTrue) && $(isFalse) { Echo yes2 ; } else { Echo no2 ; }

if ( $(isTrue) || $(isTrue) ) && $(isFalse) { Echo yes3 ; } else { Echo no3 ; }



##if ($(isFalse)) {
##Echo this is false ;
##}
###if $(isFalse)||$(isTrue) { # This is a combine expression, not a boolean.
###Echo this is NOT true ; ## !!!!!!!!
###}
if $(isFalse) || $(isTrue) { # Note whitespace
  Echo this is true ; ## :)
}
if $(isTrue)(a { # This is a combine expression showing parens is not a token at low-level.
  Echo this is true as well ;
}

# The following two are equivalent. Shows that the first case is not a parenthesized expression
# as you'd expect.
##if ($(isFalse) || $(isTrue)) { Echo aa ; }
##if x$(isFalse) || $(isTrue)) { Echo aa ; }

##if $(isFalse) && ($(isFalse) || $(isTrue)) {
##Echo combined ;
##}

##if )( = )( {
##Echo also wat? ;
##}

###if (x != x) {
###Echo wat? ;
###}
###if (x = (x {
###Echo this is what you meant, right? ;
###}

a = foo ;
if $(a)x = foox {
  Echo this one prints ;
}
if ($(a)x = foox) { # Just combine expression! Not parenthesis.
  Echo but this one does not ;
}

# Neither of these is valid even though you'd expect them to if parenthesis are not special.
# So why? Who knows....
#if ( { Echo x ; }
#if ) { Echo x ; }

if () {
  Echo this is just a literal with two characters ;
}

# This does not parse in Jam!
#Echo ) ;
"
            );
		}

		[Test]
		public void VariableExpansionInString()
		{
			AssertConvertedProgramHasIdenticalOutput(
@"
myvar = harry ;
Echo ""bla$(myvar)bla"" ;
Echo ""bla\\$\\(myvar\\)bla"" ; "
			);
		}

        [Test]
        public void Subtract()
        {
            AssertConvertedProgramHasIdenticalOutput(@"
myvar = one two three ;

myvar2 = tw ;
myvar3 = o ;
myvar4 = $(myvar2)$(myvar3) ;
myvar -= $(myvar4) ;
Echo $(myvar) ;
");
        }

        [Test]
        public void SingleValueIsIn()
        {
            AssertConvertedProgramHasIdenticalOutput(@"
if harry in harry sally johny { Echo yes ; } else { Echo no ; }
");
        }


        [Test]
		public void OnTargetVariables()
		{
			AssertConvertedProgramHasIdenticalOutput(
@"
myvar on harry = sally ;
myvar = 3 ;
myothervar = 5 ;
Echo $(myvar) ;

on harry { 
  Echo $(myvar) ;
  Echo $(myothervar) ;
  
  #today we have different semantics for writing to variable that exists on a target in an on block
  #we think and hope that we do not rely on this semantic in our jam program anywhere.
  #myvar = johny ;

  myothervar = 8 ;
}

Echo marker $(myvar) ;
Echo $(myothervar) ;

on harry {
  Echo $(myvar) ;
}

on doesNotExist {
  Echo $(myvar) from doesNotExist ;
}

rule GreenGoblin
{
  Echo FromGreenGoblin ;
  return greengoblin ;
}

Echo luca slucas lucas ;
mytargets = superman spiderman ;
myvar on $(mytargets) = [ GreenGoblin ] ;
myvar on $(mytargets) += uh oh ;
myvar on superman += onlyonsuperman ;
myvar on spiderman += onlyonspiderman ;

myvar_only_spiderman on spiderman = catwoman ;

on superman {
  Echo $(myvar) ;
}
on spiderman {
  Echo $(myvar) ;
}

on $(mytargets) {
    Echo $(myvar) ;
    Echo $(myvar_only_spiderman) ;
}


Echo valid ;
myvar = 3 ;
mads = myvar myvar2 ;
$(mads) on mytarget = 2 ;
containsmytarget = mytarget  ;
on $(containsmytarget) { Echo $(myvar) ;  Echo $(myvar2) ; }

rule ReturnEmpty { return ; }
empty = [ ReturnEmpty ] ;
on $(empty) { }


");
		}

		[Test]
		public void LiteralExpansion()
		{
			AssertConvertedProgramHasIdenticalOutput(
@"
Echo @(harry:S=.exe) ;

myvar = hello there ;
Echo @($(myvar)/somepath:S=.ini) ;
"
			);
		}

      

		[Test]
		public void Include()
		{
			var jam1 =
@"
Echo this file will be csharp ;
myvar = file2.jam ;
include $(myvar) ;
Echo file1 post ;
";
			var jam2 =
@"
Echo this file will stay jam ;
include file3.jam ;
Echo file2 post ;
";

			var jam3 =
@"
Echo and this file is csharp again ;
";

			var jamProgram = new ProgramDescripton()
			{
				new SourceFileDescription() { Contents = jam1, File = new NPath("file1.jam") },
				new SourceFileDescription() { Contents = jam2, File = new NPath("file2.jam") },
				new SourceFileDescription() { Contents = jam3, File = new NPath("file3.jam") }
			};
			
			AssertConvertedProgramHasIdenticalOutput(jamProgram, new[] { "file1.jam", "file3.jam"});
		}

        [Test]
        public void IncludedFileReturns()
        {
            var jam1 =
@"
Echo file1 ;
include file2.jam ;
Echo file1 post ;
";
            var jam2 =
@"
Echo file2 ;
#return ;
Echo file2 post ;
";

            var jamProgram = new ProgramDescripton()
            {
                new SourceFileDescription() { Contents = jam1, File = new NPath("file1.jam") },
                new SourceFileDescription() { Contents = jam2, File = new NPath("file2.jam") },
            };

            AssertConvertedProgramHasIdenticalOutput(jamProgram);
        }

        [Test]
        public void VariablePersistence()
        {
            var jam1 =
@"
Echo this file will be csharp ;

myglobal = 123 ;
Echo myglobal from csharp $(myglobal) ;
include file2.jam ;
";
            var jam2 =
@"
Echo this file will stay jam ;
Echo myglobal from jam $(myglobal) ;
myglobal = 312 ;
include file3.jam ;
";

            var jam3 =
@"
Echo and this file is csharp again ;
Echo myglobal from file3 $(myglobal) ;
";

            var jamProgram = new ProgramDescripton()
            {
                new SourceFileDescription() { Contents = jam1, File = new NPath("file1.jam") },
                new SourceFileDescription() { Contents = jam2, File = new NPath("file2.jam") },
                new SourceFileDescription() { Contents = jam3, File = new NPath("file3.jam") }
            };

            AssertConvertedProgramHasIdenticalOutput(jamProgram, new[] { "file1.jam", "file3.jam" });
        }

        private static void AssertConvertedProgramHasIdenticalOutput(string simpleProgram)
	    {
		    AssertConvertedProgramHasIdenticalOutput(new ProgramDescripton {new SourceFileDescription() {File = new NPath("Jamfile.jam"), Contents = simpleProgram}});
	    }

	    private static void AssertConvertedProgramHasIdenticalOutput(ProgramDescripton program, IEnumerable<string> onlyConvert = null)
	    {
		    program[0].Contents = "NotFile all ;\n" + program[0].Contents;

		    var jamRunInstructions = new JamRunnerInstructions {JamfilesToCreate = program};

			var jamResult = new JamRunner().Run(jamRunInstructions).Select(s => s.TrimEnd());
		    Console.WriteLine("Jam:");
		    foreach (var l in jamResult)
			    Console.WriteLine(l);

		    IEnumerable<string> csharpResult = null;

	        Func<NPath, bool> shouldConvert = (NPath name) => onlyConvert == null || onlyConvert.Contains(name.ToString());

		    try
		    {
		        var toBeCSharp = new ProgramDescripton(program.Where(f => shouldConvert(f.File)));
                var toStayJam  = new ProgramDescripton(program.Where(f => !shouldConvert(f.File)));

                var csharp = new JamToCSharpConverter().Convert(toBeCSharp);

			    var convertedJamRunInstructions = new JamRunnerInstructions()
			    {
				    CSharpFiles = csharp,
				    JamfilesToCreate = toStayJam,
				    JamFileToInvokeOnStartup = program[0].File.FileName
			    };

			    csharpResult = new JamRunner().Run(convertedJamRunInstructions).Select(s => s.TrimEnd());

			    Console.WriteLine("C#:");
			    foreach (var l in csharpResult)
				    Console.WriteLine(l);
			    Console.WriteLine();
		    }
		    catch (Exception e)
		    {
			    Console.WriteLine("Failed converting/running to c#: " + e);
		    }
		    CollectionAssert.AreEqual(jamResult, csharpResult);
	    }
    }
}