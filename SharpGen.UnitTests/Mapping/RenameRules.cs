﻿using SharpGen.CppModel;
using SharpGen.Model;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace SharpGen.UnitTests.Mapping
{
    public class RenameRules : MappingTestBase
    {
        public RenameRules(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        [Fact]
        public void MappingNameRuleRenamesStruct()
        {
            var config = new Config.ConfigFile
            {
                Id = nameof(MappingNameRuleRenamesStruct),
                Namespace = nameof(MappingNameRuleRenamesStruct),
                Assembly = nameof(MappingNameRuleRenamesStruct),
                Includes =
                {
                    new Config.IncludeRule
                    {
                        File = "simpleStruct.h",
                        Attach = true,
                        Namespace = nameof(MappingNameRuleRenamesStruct)
                    }
                },
                Bindings =
                {
                    new Config.BindRule("int", "System.Int32")
                },
                Mappings =
                {
                    new Config.MappingRule
                    {
                        Struct = "Test",
                        MappingName = "MyStruct"
                    }
                }
            };

            var cppStruct = new CppStruct
            {
                Name = "Test"
            };

            cppStruct.Add(new CppField
            {
                Name = "field",
                TypeName = "int"
            });

            var include = new CppInclude
            {
                Name = "simpleStruct"
            };
            include.Add(cppStruct);
            var module = new CppModule();
            module.Add(include);

            var (solution, _) = MapModel(module, config);

            Assert.Single(solution.EnumerateDescendants().OfType<CsStruct>().Where(element => element.Name == "MyStruct"));
        }

        [Fact]
        public void MappingNameRuleRenamesStructMember()
        {
            var config = new Config.ConfigFile
            {
                Id = nameof(MappingNameRuleRenamesStructMember),
                Namespace = nameof(MappingNameRuleRenamesStructMember),
                Assembly = nameof(MappingNameRuleRenamesStructMember),
                Includes =
                {
                    new Config.IncludeRule
                    {
                        File = "simpleStruct.h",
                        Attach = true,
                        Namespace = nameof(MappingNameRuleRenamesStructMember)
                    }
                },
                Bindings =
                {
                    new Config.BindRule("int", "System.Int32")
                },
                Mappings =
                {
                    new Config.MappingRule
                    {
                        Field = "Test::field",
                        MappingName = "MyField"
                    }
                }
            };

            var cppStruct = new CppStruct
            {
                Name = "Test"
            };

            cppStruct.Add(new CppField
            {
                Name = "field",
                TypeName = "int"
            });

            var include = new CppInclude
            {
                Name = "simpleStruct"
            };
            include.Add(cppStruct);
            var module = new CppModule();
            module.Add(include);

            var (solution, _) = MapModel(module, config);

            var csStruct = solution.EnumerateDescendants().OfType<CsStruct>().First(element => element.Name == "Test");

            Assert.Single(csStruct.Fields.Where(field => field.Name == "MyField"));
        }
    }
}
