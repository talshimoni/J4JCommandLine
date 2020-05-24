﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using J4JSoftware.Logging;

namespace J4JSoftware.CommandLine
{
    public class BindingTarget<TValue> : IBindingTarget<TValue>
        where TValue : class
    {
        private readonly IEnumerable<ITextConverter> _converters;
        private readonly CommandLineErrors _errors;
        private readonly StringComparison _keyComp;
        private readonly IJ4JLogger? _logger;
        private readonly Func<IJ4JLogger>? _loggerFactory;
        private readonly IOptionCollection _options;
        private readonly List<TargetedProperty> _properties = new List<TargetedProperty>();
        private readonly ITargetableTypeFactory _targetableTypeFactory;

        public BindingTarget(
            string targetID,
            TValue value,
            IEnumerable<ITextConverter> converters,
            IOptionCollection options,
            IParsingConfiguration parseConfig,
            CommandLineErrors errors,
            Func<IJ4JLogger>? loggerFactory = null
        )
        {
            ID = targetID;
            Value = value;
            _converters = converters;
            _options = options;
            _errors = errors;
            _loggerFactory = loggerFactory;

            _logger = _loggerFactory?.Invoke();
            _logger?.SetLoggedType( GetType() );

            _keyComp = parseConfig.TextComparison;
            _targetableTypeFactory = new TargetableTypeFactory( _converters, loggerFactory?.Invoke() );
            //Initialize();
        }

        //public BindingTarget(
        //    string targetID,
        //    IEnumerable<ITextConverter> converters,
        //    IOptionCollection options,
        //    IParsingConfiguration parseConfig,
        //    CommandLineErrors errors,
        //    Func<IJ4JLogger>? loggerFactory = null
        //)
        //{
        //    ID = targetID;
        //    _converters = converters;
        //    _options = options;
        //    _parseConfig = parseConfig;
        //    _errors = errors;
        //    _loggerFactory = loggerFactory;

        //    _logger = _loggerFactory?.Invoke();
        //    _logger?.SetLoggedType( GetType() );

        //    // if TTarget can't be created we have to abort
        //    if( !typeof(TValue).HasPublicParameterlessConstructor() )
        //        throw new ApplicationException( $"Couldn't create and instance of {typeof(TValue)}" );

        //    Value = Activator.CreateInstance<TValue>();

        //    _keyComp = parseConfig.TextComparison;

        //    //Initialize();
        //}

        public TValue Value { get; }
        public string ID { get; }
        public ReadOnlyCollection<TargetedProperty> TargetableProperties => _properties.ToList().AsReadOnly();

        public OptionBase Bind<TProp>(
            Expression<Func<TValue, TProp>> propertySelector,
            params string[] keys )
        {
            var propType = typeof(TProp);
            var pathElements = propertySelector.GetPropertyPathInfo();

            TargetedProperty? property = null;

            foreach( var pathElement in pathElements )
            {
                property = new TargetedProperty(
                    pathElement,
                    Value,
                    property,
                    _targetableTypeFactory,
                    _keyComp,
                    _loggerFactory?.Invoke()
                );
            }

            if( property == null )
            {
                AddError(keys.First(), $"Final TargetProperty is undefined");
                return new NullOption( _options, _loggerFactory?.Invoke() );
            }

            _properties.Add( property );

            // only certain types of collections are handled, and they are handled
            // differently from single-valued properties. Also, all single-valued properties
            // must have an associated ITextConverter
            if ( propType.IsArray )
            {
                var elementType = propType.GetElementType()!;

                if( _converters.Any( c => c.SupportedType == elementType ) )
                    return CreateCollectionOption( property, keys );
            }

            if( propType.IsGenericType 
                && typeof(IList).IsAssignableFrom(propType) 
                && propType.GenericTypeArguments.Length == 1 )
            {
                var elementType = propType.GenericTypeArguments[0];

                if ( _converters.Any( c => c.SupportedType == elementType ) )
                    return CreateCollectionOption(property, keys);
            }

            if (_converters.Any(c => c.SupportedType == propType))
                return CreateSingleOption( property, keys );

            // create a NullOption
            return FinalizeAndStoreOption( property, null, keys );
        }

        //public OptionBase BindCollection<TProp>(
        //    Expression<Func<TValue, TProp>> propertySelector,
        //    TProp defaultValue,
        //    params string[] keys )
        //    => CreateSingleOption<TProp>( propertySelector.GetPropertyPathInfo(), keys, defaultValue );

        //public OptionBase BindCollection<TProp>(
        //    Expression<Func<TValue, TProp>> propertySelector,
        //    IEnumerable<TProp> defaultValue,
        //    params string[] keys )
        //    => CreateCollectionOption<TProp>( propertySelector.GetPropertyPathInfo(), keys, defaultValue );

        //public OptionBase BindProperty(
        //    string propertyPath,
        //    object? defaultValue,
        //    params string[] keys ) => CreateSingleOption( propertyPath, keys, defaultValue );

        //public OptionBase BindPropertyCollection(
        //    string propertyPath,
        //    params string[] keys ) => CreateCollectionOption( propertyPath, keys );

        public MappingResults MapParseResults( ParseResults parseResults )
        {
            var retVal = MappingResults.Success;

            // scan all the bound options that aren't tied to NullOptions, which are only
            // "bound" in error
            foreach( var property in _properties )
            {
                switch( property.BoundOption!.OptionType )
                {
                    case OptionType.Help:
                        if( property.MapParseResult( this, parseResults, _logger ) == MappingResults.Success )
                            retVal |= MappingResults.HelpRequested;
                        break;

                    case OptionType.Mappable:
                        retVal |= property.MapParseResult(this, parseResults, _logger);
                        break;

                    case OptionType.Null:
                        retVal |= MappingResults.Unbound;
                        break;
                }
            }

            return retVal;
        }

        public void AddError( string key, string error )
        {
            _errors.AddError( this, key, error );
        }

        object IBindingTarget.GetValue()
        {
            return Value;
        }

        //private void Initialize()
        //{
        //    var type = typeof(TValue);

        //    _logger?.Verbose( "Finding targetable properties for {type}", type );

        //    ScanProperties( type, Value, new Stack<PropertyInfo>() );
        //}

        //private void ScanProperties<TScan>( Type containerType, TScan container, Stack<PropertyInfo> pathToContainer )
        //{
        //    foreach( var property in containerType.GetProperties() )
        //    {
        //        var curTP = TargetedProperty.Create( property, container, _keyComp, pathToContainer, _logger );

        //        if( !curTP.IsTargetable )
        //        {
        //            _logger?.Verbose<string>( "Property {0} is not targetable", property.Name );
        //            continue;
        //        }

        //        _properties.Add( curTP );

        //        // if the property isn't defined in the container, create it so we can 
        //        // traverse any properties it may have
        //        object? child = null;

        //        if( curTP.IsDefined )
        //        {
        //            child = property.GetValue( container );
        //        }
        //        else
        //        {
        //            // we only need to create properties that are SingleValues because
        //            // those are the only ones we recurse into looking for targetable properties
        //            if( curTP.Multiplicity == PropertyMultiplicity.SingleValue )
        //            {
        //                child = Activator.CreateInstance( property.PropertyType );
        //                property.SetValue( container, child );
        //            }
        //        }

        //        _logger?.Information<string>( "Found targetable property {0}", property.Name );

        //        // recurse over any child properties of the current property provided it's a
        //        // SingleValue property but not a ValueType (which don't have child properties)
        //        if( curTP.Multiplicity == PropertyMultiplicity.SingleValue
        //            && !typeof(ValueType).IsAssignableFrom( property.PropertyType ) )
        //        {
        //            _logger?.Verbose<string>( "Finding targetable properties and methods for {0}", property.Name );

        //            pathToContainer.Push( property );

        //            ScanProperties( property.PropertyType, child, pathToContainer );
        //        }
        //    }

        //    if( pathToContainer.Count > 0 )
        //        pathToContainer.Pop();
        //}

        // Creates an Option based on a supported single value property
        // The properties must be ordered from "root" to "leaf" (i.e., from the ultimate parent property to the property
        // being targeted).
        private OptionBase CreateSingleOption( TargetedProperty property, string[] keys )
        {
            OptionBase? retVal = null;

            //if( property == null )
            //{
            //    _logger?.Error<string>(
            //        "Attempted to bind to complex property '{0}', which is not supported", propertyPath );

            //    property = new TargetedProperty(_keyComp, _properties.Count + 1 );
            //    _properties.Add( property );

            //    var key = keys.FirstOrDefault() ?? "?";
            //    AddError(key, $"Attempted to bind to complex property '{propertyPath}', which is not supported");

            //    return FinalizeAndStoreOption(property, retVal, keys);
            //}

            // check that it's the right multiplicity (this method only handles single-valued properties)
            if (!property.Multiplicity.IsTargetableSingleValue())
                _logger?.Error<string>( "Property '{propertyPath}' is not single-valued", property.FullPath );
            else
                retVal = CreateOption( property.PropertyInfo.PropertyType );

            return FinalizeAndStoreOption( property, retVal, keys );
        }

        // Creates an Option based on a supported collection property
        // The properties must be ordered from "root" to "leaf" (i.e., from the ultimate parent property to the property
        // being targeted).
        private OptionBase CreateCollectionOption(TargetedProperty property, string[] keys )
        {
            OptionBase? retVal = null;

            //var property = TargetedProperty.Create(properties, Value, _keyComp, _logger);
            //_properties.Add(property);

            //if ( property == null )
            //{
            //    _logger?.Error<string>(
            //        "Attempted to bind to complex property '{0}', which is not supported", propertyPath);

            //    property = new TargetedProperty(_keyComp, _properties.Count + 1);
            //    _properties.Add(property);

            //    var key = keys.FirstOrDefault() ?? "?";
            //    AddError(key, $"Attempted to bind to complex property '{propertyPath}', which is not supported");

            //    return FinalizeAndStoreOption(property, retVal, keys);
            //}

            // check that it's the right multiplicity (this method only handles collection properties)
            if ( !property.Multiplicity.IsTargetableCollection() )
                _logger?.Error<string>( "Property '{propertyPath}' is not a supported collection", property.FullPath );
            else
            {
                // we need to find the Type on which the collection is based
                var elementType = property.Multiplicity == PropertyMultiplicity.Array
                    ? property.PropertyInfo.PropertyType.GetElementType()
                    : property.PropertyInfo.PropertyType.GenericTypeArguments.First();

                retVal = CreateOption( elementType! );
            }

            return FinalizeAndStoreOption( property, retVal, keys );
        }

        // creates an option for the specified Type provided an ITextConverter for
        // that Type exists. Otherwise, returns a NullOption
        private OptionBase CreateOption(Type propType)
        {
            OptionBase? retVal = null;

            var converter = _converters.FirstOrDefault(c => c.SupportedType == propType);

            if( converter == null )
                _logger?.Error( "No ITextConverter exists for Type {0}", propType );
            else
                retVal = new Option( 
                    _options, 
                    converter, 
                    _targetableTypeFactory.Create( propType ),
                    _loggerFactory?.Invoke() );

            return retVal ?? new NullOption(_options, _loggerFactory?.Invoke());
        }

        // ensures option exists and has at least one valid, unique key.
        // returns a NullOption if not. Stores the new option in the options
        // collection.
        private OptionBase FinalizeAndStoreOption( TargetedProperty property, OptionBase? option, string[] keys )
        {
            keys = _options.GetUniqueKeys( keys );

            if( keys.Length == 0 )
                _logger?.Error( "No unique keys defined for Option" );

            if( keys.Length == 0 || option == null )
                option = new NullOption( _options, _loggerFactory?.Invoke() );

            option.AddKeys( keys );

            _options.Add( option );

            property.BoundOption = option;

            return option;
        }
    }
}