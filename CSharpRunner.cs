﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using jamconverter.Tests;
using NiceIO;
using NUnit.Framework;
using Unity.IL2CPP;

namespace jamconverter
{
    class CSharpRunner
    {
        public string[] Run(ProgramDescripton program, IEnumerable<NPath> additionalLibs = null)
        {
            var executable = Compile(program, additionalLibs);

            return Shell.Execute(executable, "").Split(new[] {Environment.NewLine}, StringSplitOptions.None);
        }

        public static NPath Compile(ProgramDescripton program, IEnumerable<NPath> additionalLibs, NPath outputFile = null)
        {
            var executable = outputFile ??  NPath.CreateTempDirectory("CSharp").Combine("program.exe");
            var tmpDir = executable.Parent;

            var absoluteCSFiles = new List<NPath>();
            foreach (var fileEntry in program)
            {
                var absolutePath = tmpDir.Combine(fileEntry.FileName);
                absoluteCSFiles.Add(absolutePath);
                var file = absolutePath.WriteAllText(fileEntry.Contents);
                Console.WriteLine(".cs: " + file);
            }

	        var csproj = tmpDir.Combine("program.csproj");
	        csproj.WriteAllText(CSProjContentsFor(program, additionalLibs));
			Console.WriteLine("csproj: "+csproj);

            var compiler = new NPath(@"C:\il2cpp-dependencies\MonoBleedingEdge\builds\monodistribution\bin\mcs" + (Environment.OSVersion.Platform == PlatformID.Win32NT ? ".bat" : ""));
            
            if (additionalLibs == null) additionalLibs = new NPath[0];

            Shell.Execute(compiler, absoluteCSFiles.InQuotes().SeperateWithSpace() + " " + additionalLibs.InQuotes().Select(l => "-r:" + l).SeperateWithSpace() + " -debug -langversion:6 -out:" + executable);

            foreach (var lib in additionalLibs)
                lib.Copy(tmpDir);
            return executable;
        }

	    private static string CSProjContentsFor(ProgramDescripton program, IEnumerable<NPath> additionalLibs)
	    {
		    var template = ReadTemplate();

		    var inject = new StringBuilder();
		    inject.AppendLine("<ItemGroup>");
		    inject.AppendLine(@" <Reference Include=""System"" />");
			inject.AppendLine(@" <Reference Include=""System.Core"" />");
		    foreach (var additionalLib in additionalLibs)
			    inject.AppendLine($@"  <Reference Include=""{additionalLib}"" />");
			inject.AppendLine("</ItemGroup>");

			inject.AppendLine("<ItemGroup>");
			foreach (var file in program)
				inject.AppendLine($@"  <Compile Include=""{file.FileName}"" />");
			inject.AppendLine("</ItemGroup>");

		    return template.Replace("$$INSERT_FILES_HERE$$", inject.ToString());
	    }

	    private static string ReadTemplate()
	    {
		    var assembly = typeof(CSharpRunner).Assembly;
		    using (Stream resFilestream = assembly.GetManifestResourceStream(typeof(CSharpRunner), "csproj_template"))
		    {
			    byte[] ba = new byte[resFilestream.Length];
			    resFilestream.Read(ba, 0, ba.Length);
			    return Encoding.UTF8.GetString(ba);
		    }
	    }
    }

    [TestFixture]
    class CSharpRunnerTests
    {
        [Test]
        public void CanRunSimpleProgram()
        {
            var program = @"
class Dummy {
    static void Main()
    {
      System.Console.WriteLine(""Hello!"");
    }
}
";

            var output = new CSharpRunner().Run(new ProgramDescripton() { new SourceFileDescription() { Contents = program, FileName = "Main.cs" }});
            CollectionAssert.AreEqual(new[] {"Hello!"}, output);
        }
    }
}
