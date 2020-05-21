﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using J4JSoftware.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace J4JCommandLine.Tests
{
    public class SubLevelTests
    {
        public class SimpleChildProperties
        {
            public string TextProperty { get; set; }
            public int IntProperty { get; set; }
            public bool BoolProperty { get; set; }
            public decimal DecimalProperty { get; set; }
            public List<int> IntList { get; set; }
            public int[] IntArray { get; set; }
        }

        public class ComplexChildProperties
        {
            public ComplexChildProperties( int someValue )
            {
            }

            public string TextProperty { get; set; }
            public int IntProperty { get; set; }
            public bool BoolProperty { get; set; }
            public decimal DecimalProperty { get; set; }
            public List<int> IntList { get; set; }
            public int[] IntArray { get; set; }
        }

        public class RootProperties
        {
            public SimpleChildProperties SimpleChildProperties { get; set; }
            public ComplexChildProperties ComplexChildProperties { get; set; }
        }

        private readonly StringWriter _consoleWriter = new StringWriter();
        private readonly TextConverter _textConv = new TextConverter();

        public SubLevelTests()
        {
            Console.SetOut( _consoleWriter );
        }

        [ Theory ]
        [ InlineData( "z", "32", "SimpleChildProperties.IntProperty", "-1", MappingResults.MissingRequired, "-1" ) ]
        [ InlineData( "x", "123.456", "SimpleChildProperties.DecimalProperty", "0", MappingResults.Success ) ]
        [InlineData("x", "32", "SimpleChildProperties.IntProperty", "-1", MappingResults.Success)]
        public void Simple_properties(
            string key,
            string arg,
            string propToTest,
            string defaultValue,
            MappingResults result,
            string? propValue = null )
        {
            propValue ??= arg;
            propToTest.Should().NotBeNullOrEmpty();

            var context = TestServiceProvider.Instance.GetRequiredService<CommandLineContext>();

            var target = context.AddBindingTarget( new RootProperties(), "test" );

            target.TargetableProperties.Should()
                .Contain( x => string.Equals( x.FullPath, propToTest, StringComparison.OrdinalIgnoreCase ) );

            var boundProp = target.TargetableProperties
                .First( x => string.Equals( x.FullPath, propToTest, StringComparison.OrdinalIgnoreCase ) );

            var desiredValue = _textConv.Convert( boundProp.PropertyInfo.PropertyType, propValue );
            var defValue = _textConv.Convert( boundProp.PropertyInfo.PropertyType, defaultValue );

            var option = target.BindProperty( propToTest, defValue, "x" );
            option.Required();

            var parseResult = context.Parse( new string[] { $"-{key}", arg } );

            var consoleText = _consoleWriter.ToString();

            parseResult.Should().Be( result );

            var subProp = target.Value.SimpleChildProperties;
            var boundValue = boundProp.PropertyInfo.GetValue( subProp );

            boundValue.Should().NotBeNull();
            boundValue.Should().Be( desiredValue );
        }

        [Theory]
        [InlineData("x", "32", "-1")]
        public void Complex_properties_single(
            string key,
            string arg,
            string defaultValue)
        {
            var context = TestServiceProvider.Instance.GetRequiredService<CommandLineContext>();

            var target = context.AddBindingTarget(new RootProperties(), "test");

            target.TargetableProperties.Should()
                .NotContain( x => string.Equals( x.FullPath, "ComplexChildProperties.IntProperty",
                    StringComparison.OrdinalIgnoreCase ) );

            var option = target.BindProperty( 
                x => x.ComplexChildProperties.IntProperty,
                Convert.ToInt32( defaultValue ), 
                "x" );

            var parseResult = context.Parse(new string[] { $"-{key}", arg });

            var consoleText = _consoleWriter.ToString();

            parseResult.Should().Be( MappingResults.Unbound );

            var boundValue = target.Value.ComplexChildProperties;

            boundValue.Should().BeNull();
        }

        [Theory]
        [InlineData("x", "32")]
        public void Complex_properties_collection(
            string key,
            string arg)
        {
            var context = TestServiceProvider.Instance.GetRequiredService<CommandLineContext>();

            var target = context.AddBindingTarget(new RootProperties(), "test");

            target.TargetableProperties.Should()
                .NotContain(x => string.Equals(x.FullPath, "ComplexChildProperties.IntList",
                    StringComparison.OrdinalIgnoreCase));

            var option = target.BindProperty(
                x => x.ComplexChildProperties.IntList,
                new List<int>(),
                "x");

            var parseResult = context.Parse(new string[] { $"-{key}", arg });

            var consoleText = _consoleWriter.ToString();

            parseResult.Should().Be(MappingResults.Unbound);

            var boundValue = target.Value.ComplexChildProperties;

            boundValue.Should().BeNull();
        }

        [Theory]
        [InlineData("x", "32", false, MappingResults.Success)]
        [InlineData("z", "32", true, MappingResults.MissingRequired, "-1")]
        [InlineData("z", "32", false, MappingResults.Success, "-1")]
        public void Is_required(
            string key,
            string arg,
            bool required,
            MappingResults result,
            string? propValue = null)
        {
            propValue ??= arg;

            var context = TestServiceProvider.Instance.GetRequiredService<CommandLineContext>();

            var target = context.AddBindingTarget(new RootProperties(), "test");

            target.TargetableProperties.Should()
                .Contain(x => string.Equals(x.FullPath, "SimpleChildProperties.IntProperty", StringComparison.OrdinalIgnoreCase));

            var boundProp = target.TargetableProperties
                .First(x => string.Equals(x.FullPath, "SimpleChildProperties.IntProperty", StringComparison.OrdinalIgnoreCase));

            var desiredValue = _textConv.Convert(boundProp.PropertyInfo.PropertyType, propValue);

            var option = target.BindProperty( x => x.SimpleChildProperties.IntProperty, -1, "x" );

            if( required ) option.Required();
            else option.Optional();

            var parseResult = context.Parse(new string[] { $"-{key}", arg });

            var consoleText = _consoleWriter.ToString();

            parseResult.Should().Be(result);

            var subProp = target.Value.SimpleChildProperties;
            var boundValue = boundProp.PropertyInfo.GetValue(subProp);

            boundValue.Should().NotBeNull();
            boundValue.Should().Be(desiredValue);
        }

        [Theory]
        [InlineData(new string[]{ "32"}, 0, Int32.MaxValue, MappingResults.Success)]
        [InlineData(new string[] { "32" }, 2, Int32.MaxValue, MappingResults.TooFewParameters)]
        [InlineData(new string[] { "32" }, 0, 0, MappingResults.TooManyParameters)]
        public void Num_parameters_list(
            string[] rawArgs,
            int minArgs,
            int maxArgs,
            MappingResults result)
        {
            var context = TestServiceProvider.Instance.GetRequiredService<CommandLineContext>();

            var target = context.AddBindingTarget(new RootProperties(), "test");

            var option = target.BindProperty(x => x.SimpleChildProperties.IntList, null, "x");
            option.Should().BeAssignableTo<Option>();

            option.ArgumentCount( minArgs, maxArgs );

            var args = rawArgs.ToList();
            args.Insert(0, "-x");

            var parseResult = context.Parse( args.ToArray() );

            var consoleText = _consoleWriter.ToString();

            parseResult.Should().Be(result);

            var expectedValues = new List<int>();
            var lowerLimit = minArgs > rawArgs.Length ? minArgs : 0;
            var upperLimit = maxArgs > rawArgs.Length ? rawArgs.Length : maxArgs;

            for( var idx = lowerLimit; idx < upperLimit; idx++ )
            {
                expectedValues.Add( Convert.ToInt32( rawArgs[ idx ] ) );
            }

            target.Value.SimpleChildProperties.IntList.Should().NotBeNull();
            target.Value.SimpleChildProperties.IntList.Count.Should().Be( expectedValues.Count );
            target.Value.SimpleChildProperties.IntList.Should().BeEquivalentTo( expectedValues );
        }

        [Theory]
        [InlineData(new string[] { "32" }, 0, Int32.MaxValue, MappingResults.Success)]
        [InlineData(new string[] { "32" }, 2, Int32.MaxValue, MappingResults.TooFewParameters)]
        [InlineData(new string[] { "32" }, 0, 0, MappingResults.TooManyParameters)]
        public void Num_parameters_array(
            string[] rawArgs,
            int minArgs,
            int maxArgs,
            MappingResults result)
        {
            var context = TestServiceProvider.Instance.GetRequiredService<CommandLineContext>();

            var target = context.AddBindingTarget(new RootProperties(), "test");

            var option = target.BindProperty(x => x.SimpleChildProperties.IntArray, null, "x");
            option.Should().BeAssignableTo<Option>();

            option.ArgumentCount(minArgs, maxArgs);

            var args = rawArgs.ToList();
            args.Insert(0, "-x");

            var parseResult = context.Parse(args.ToArray());

            var consoleText = _consoleWriter.ToString();

            parseResult.Should().Be(result);

            var expectedValues = new List<int>();
            var lowerLimit = minArgs > rawArgs.Length ? minArgs : 0;
            var upperLimit = maxArgs > rawArgs.Length ? rawArgs.Length : maxArgs;

            for (var idx = lowerLimit; idx < upperLimit; idx++)
            {
                expectedValues.Add(Convert.ToInt32(rawArgs[idx]));
            }

            target.Value.SimpleChildProperties.IntArray.Should().NotBeNull();
            target.Value.SimpleChildProperties.IntArray.Length.Should().Be(expectedValues.Count);
            target.Value.SimpleChildProperties.IntArray.Should().BeEquivalentTo(expectedValues.ToArray());
        }
    }
}
