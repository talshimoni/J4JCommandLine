﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace J4JSoftware.CommandLine
{
    // defines target for binding operations, tying command line arguments to
    // specific properties of TValue
    public class BindingTarget<TValue> : IBindingTarget<TValue>
        where TValue : class
    {
        private readonly List<TargetedProperty> _properties = new List<TargetedProperty>();

        private bool _headerDisplayed = false;
        private bool _helpDisplayed = false;
        private bool _outputPending = false;

        // creates an instance tied to the supplied instance of TValue. This allows for binding
        // to more complex objects which may require constructor parameters.
        internal BindingTarget()
        {
        }

        internal IAllocator Allocator { get; set; }
        internal IEnumerable<ITextConverter> Converters { get; set; }
        internal ITargetableTypeFactory TypeFactory { get; set; }
        internal OptionCollection Options { get; set; }
        public CommandLineLogger Logger { get; internal set; }
        internal StringComparison TextComparison { get; set; }
        internal MasterTextCollection MasterText { get; set; }
        internal IConsoleOutput ConsoleOutput { get; set; }

        public bool IsConfigured => Allocator != null && Converters != null && TypeFactory != null && Options != null 
                                    && Logger != null && MasterText != null && ConsoleOutput != null;

        public bool IgnoreUnkeyedParameters { get; internal set; }

        // The instance of TValue being bound to, which was either supplied in the constructor to 
        // this instance or created by it if TValue has a public parameterless constructor
        public TValue Value { get; internal set; }

        public string ProgramName { get; internal set; }
        public string Description { get; internal set; }

        public bool Initialize()
        {
            _properties.Clear();
            
            Logger.Clear();
            Options.Clear();

            _headerDisplayed = false;
            _outputPending = false;
            _helpDisplayed = false;

            return IsConfigured;
        }

        // binds the selected property to a newly-created Option instance. If all goes
        // well that will be an Option object capable of being a valid parsing target. If
        // something goes wrong a NullOption object will be returned. These only serve
        // to capture error information about the binding and parsing efforts.
        //
        // There are a number of reasons why a selected property may not be able to be bound
        // to an Option object. Examples: the property is not publicly read- and write-able; 
        // the property has a null value and does not have a public parameterless constructor
        // to create an instance of it. Check the error output after parsing for details.
        public Option Bind<TProp>(
            Expression<Func<TValue, TProp>> propertySelector,
            params string[] keys )
        {
            if( !IsConfigured )
                return GetUntargetedOption( keys, null, $"{this.GetType().Name} is not configured" );

            // determine whether we were given at least one valid, unique (i.e., so far
            // unused) key
            keys = Options.GetUniqueKeys(keys);

            if( keys.Length == 0 )
                return GetUntargetedOption( keys, null, $"No unique keys defined" );

            var property = GetTargetedProperty(propertySelector.GetPropertyPathInfo());

            if( property.TargetableType.Converter == null )
                return GetUntargetedOption( 
                    keys, 
                    property,
                    $"No converter for {property.TargetableType.SupportedType.Name}" );
            
            var retVal = GetOption( property, true );

            retVal.AddKeys( keys );

            return retVal;
        }

        // binds the selected property to a newly-created Option instance which will enable
        // parsing of all the "non-option" text (i.e., command line parameters not associated with
        // any keyed option) to the selected property.
        //
        // If something goes wrong a NullOption object will be returned. These only serve
        // to capture error information about the binding and parsing efforts.
        //
        // There are a number of reasons why a selected property may not be able to be bound
        // to an Option object. Examples: the property is not publicly read- and write-able; 
        // the property has a null value and does not have a public parameterless constructor
        // to create an instance of it. Check the error output after parsing for details.
        public Option BindUnkeyed<TProp>( Expression<Func<TValue, TProp>> propertySelector )
        {
            if( !IsConfigured )
                return GetUntargetedOption( null, null, $"{this.GetType().Name} is not configured" );

            var property = GetTargetedProperty(propertySelector.GetPropertyPathInfo());

            if( property.TargetableType.Converter == null )
                return GetUntargetedOption( 
                    null, 
                    property,
                    $"No converter for {property.TargetableType.SupportedType.Name}" );

            return GetOption( property, false );
        }

        // Parses the command line arguments against the Option objects bound to 
        // targeted properties, or to NullOption objects to collect error information.
        public bool Parse( string[] args )
        {
            if( !IsConfigured )
            {
                Logger.LogError( ProcessingPhase.Parsing, $"{this.GetType().Name} is not configured" );

                DisplayHeader();
                DisplayErrors();
                DisplayHelp();

                return false;
            }

            var retVal = true;

            // parse the arguments into a collection of arguments keyed by the option key
            // note that there can be multiple arguments associated with any option key
            var parseResults = Allocator.AllocateCommandLine( args );

            // scan all the bound options that aren't tied to NullOptions, which are only
            // "bound" in error
            foreach( var property in _properties.Where( p =>
                p.BoundOption != null && p.BoundOption.OptionType == OptionType.Keyed ) )
            {
                // see if our BoundOption's keys match a key in the parse results so we can retrieve a
                // specific IAllocation
                var parseResult = parseResults
                    .FirstOrDefault( pr => property.BoundOption!
                        .Keys.Any( k => string.Equals( k, pr.Key, TextComparison ) )
                    );

                retVal &= property.MapParseResult( this, parseResult );
            }

            // now process the unkeyed parameters, if any, provided they were bound to a targeted property
            var unkeyed = _properties.FirstOrDefault( p => p.BoundOption?.OptionType == OptionType.Unkeyed );

            if( unkeyed == null )
            {
                if( !IgnoreUnkeyedParameters && parseResults.Unkeyed.NumParameters > 0 )
                {
                    retVal = false;

                    Logger.LogError( ProcessingPhase.Parsing, $"{parseResults.Unkeyed.NumParameters:n0} unprocessed parameter(s)" );
                }
            }
            else retVal &= unkeyed.MapParseResult( this, parseResults.Unkeyed );

            // safety net
            retVal = retVal && Logger.Count == 0;

            if( !retVal )
            {
                DisplayHeader();
                DisplayErrors();
                DisplayHelp();
            }

            if( parseResults.Any(
                pr => MasterText[ TextUsageType.HelpOptionKey ].Any( hk => string.Equals( hk, pr.Key ) ) ) )
            {
                DisplayHeader();
                DisplayHelp();
            }

            if( _outputPending )
                ConsoleOutput.Display();

            Logger.Clear();

            return retVal;
        }

        //// Utility method for adding logger to the error collection. These are keyed by whatever
        //// option key (e.g., the 'x' in '-x') is associated with the error.
        //public void LogError( string? key, string error )
        //{
        //    Logger.LogError( this, key, error );
        //}

        private TargetedProperty GetTargetedProperty( List<PropertyInfo> pathElements )
        {
            TargetedProperty? retVal = null;

            // walk through the chain of PropertyInfo objects creating TargetedProperty objects
            // for each property. These objects define whether a property is targetable and, if 
            // so, how to bind an Option to it.
            foreach (var pathElement in pathElements)
            {
                retVal = new TargetedProperty(
                    pathElement,
                    Value,
                    retVal,
                    TypeFactory,
                    TextComparison
                );
            }

            if (retVal == null)
                throw new NullReferenceException($"Could not create final TargetedProperty");

            _properties.Add(retVal);

            return retVal;
        }

        private Option GetOption( TargetedProperty property, bool isKeyed )
        {
            var style = OptionStyle.SingleValued;

            if( property.TargetableType.IsCollection )
                style = OptionStyle.Collection;
            else
            {
                if( property.PropertyInfo.PropertyType == typeof(bool) )
                    style = OptionStyle.Switch;
            }

            var retVal = new MappableOption( Options, property.TargetableType, Logger, isKeyed )
            {
                OptionStyle = style
            };

            // create an Option object to bind to the "final" property (i.e., the one
            // we're trying to bind to)

            Options.Add( retVal );

            property.BoundOption = retVal;

            return retVal;
        }

        private Option GetUntargetedOption( string[]? keys, TargetedProperty? property, string error )
        {
            var retVal = new UntargetedOption( Options, Logger );

            if( keys != null )
                retVal.AddKeys( keys );

            Options.Add( retVal );

            if( property != null )
                property.BoundOption = retVal;

            Logger.LogError( ProcessingPhase.Initializing, error, option : retVal );

            return retVal;
        }

        private void DisplayHeader()
        {
            if( _headerDisplayed )
                return;

            ConsoleOutput.Initialize();

            if( !string.IsNullOrEmpty( ProgramName ) )
                ConsoleOutput.AddLine( ConsoleSection.Header, ProgramName );

            if( !string.IsNullOrEmpty( Description ) )
                ConsoleOutput.AddLine( ConsoleSection.Header, Description );

            _headerDisplayed = true;
            _outputPending = true;
        }

        private void DisplayErrors()
        {
            ConsoleOutput.AddLine( ConsoleSection.Errors, "Error(s):" );

            foreach( var consolidatedError in Logger.ConsolidateLogEvents(MasterText) )
            {
                ConsoleOutput.AddError( consolidatedError );
            }

            _outputPending = true;
        }

        private void DisplayHelp()
        {
            if( _helpDisplayed )
                return;

            var sb = new StringBuilder();

            sb.Append( "Command line options" );

            switch( TextComparison )
            {
                case StringComparison.Ordinal:
                case StringComparison.InvariantCulture:
                case StringComparison.CurrentCulture:
                    sb.Append( " (case sensitive):" );
                    break;

                default:
                    sb.Append( ":" );
                    break;
            }

            ConsoleOutput.AddLine( ConsoleSection.Help, sb.ToString() );

            foreach( var option in Options
                .OrderBy( opt => opt.FirstKey )
                .Where( opt => opt.OptionType != OptionType.Null ) )
            {
                ConsoleOutput.AddOption(
                    option.ConjugateKeys( MasterText ),
                    option.Description,
                    option.DefaultValue?.ToString() );
            }

            _helpDisplayed = true;
            _outputPending = true;
        }

        // allows retrieval of the TValue instance in a type-agnostic way
        object IBindingTarget.GetValue()
        {
            return Value;
        }
    }
}