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
        public class SubProperties
        {
            public string TextProperty { get; set; }
            public int IntProperty { get; set; }
            public bool BoolProperty { get; set; }
            public decimal DecimalProperty { get; set; }
            public List<int> IntList { get; set; }
            public int[] IntArray { get; set; }
        }

        public class RootProperties
        {
            public SubProperties SubProperties { get; set; }
        }

        private readonly StringWriter _consoleWriter = new StringWriter();
        private readonly TextConverter _textConv = new TextConverter();

        public SubLevelTests()
        {
            Console.SetOut( _consoleWriter );
        }

        [ Theory ]
        [ InlineData( "x", "32", "SubProperties.IntProperty", "-1", MappingResults.Success ) ]
        [ InlineData( "z", "32", "SubProperties.IntProperty", "-1", MappingResults.Success, "-1" ) ]
        [ InlineData( "x", "123.456", "SubProperties.DecimalProperty", "0", MappingResults.Success ) ]
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

            var parseResult = context.Parse( new string[] { $"-{key}", arg } );

            var consoleText = _consoleWriter.ToString();

            parseResult.Should().Be( result );

            var subProp = target.Value.SubProperties;
            var boundValue = boundProp.PropertyInfo.GetValue( subProp );

            boundValue.Should().NotBeNull();
            boundValue.Should().Be( desiredValue );
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
                .Contain(x => string.Equals(x.FullPath, "SubProperties.IntProperty", StringComparison.OrdinalIgnoreCase));

            var boundProp = target.TargetableProperties
                .First(x => string.Equals(x.FullPath, "SubProperties.IntProperty", StringComparison.OrdinalIgnoreCase));

            var desiredValue = _textConv.Convert(boundProp.PropertyInfo.PropertyType, propValue);

            var option = target.BindProperty( x => x.SubProperties.IntProperty, -1, "x" );

            if( required ) option.Required();
            else option.Optional();

            var parseResult = context.Parse(new string[] { $"-{key}", arg });

            var consoleText = _consoleWriter.ToString();

            parseResult.Should().Be(result);

            var subProp = target.Value.SubProperties;
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

            var option = target.BindProperty(x => x.SubProperties.IntList, null, "x");
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

            target.Value.SubProperties.IntList.Should().NotBeNull();
            target.Value.SubProperties.IntList.Count.Should().Be( expectedValues.Count );
            target.Value.SubProperties.IntList.Should().BeEquivalentTo( expectedValues );
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

            var option = target.BindProperty(x => x.SubProperties.IntArray, null, "x");
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

            target.Value.SubProperties.IntArray.Should().NotBeNull();
            target.Value.SubProperties.IntArray.Length.Should().Be(expectedValues.Count);
            target.Value.SubProperties.IntArray.Should().BeEquivalentTo(expectedValues.ToArray());
        }
    }
}