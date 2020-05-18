﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using J4JSoftware.Logging;

namespace J4JSoftware.CommandLine
{
    [Flags]
    public enum MappingResults
    {
        Unbound = 1 << 0,
        UnsupportedMultiplicity = 1 << 1,
        NoKeyFound = 1 << 2,
        ConversionFailed = 1 << 3,
        ValidationFailed = 1 << 4,

        Success = 0
    }

    public class TargetableProperty
    {
        public static TargetableProperty Create( 
            PropertyInfo propInfo, 
            object? container, 
            StringComparison keyComp,
            Stack<PropertyInfo> pathToContainer,
            IJ4JLogger? logger = null )
        {
            PropertyMultiplicity multiplicity;
            Type relevantType = propInfo.PropertyType;;

            if( propInfo.PropertyType.IsArray )
            {
                // we want to ensure we can target/create the underlying element type
                var elemType = propInfo.PropertyType.GetElementType();

                if( elemType == null )
                {
                    logger?.Error<string>( "Cannot determine element type for array {0}", propInfo.Name );
                    multiplicity = PropertyMultiplicity.Unsupported;
                }
                else multiplicity = PropertyMultiplicity.Array;
            }
            else
            {
                // see if the property type is a List<>
                if( typeof(ICollection).IsAssignableFrom( propInfo.PropertyType ) )
                {
                    if( propInfo.PropertyType.IsGenericType )
                    {
                        if( propInfo.PropertyType.GenericTypeArguments.Length == 1 )
                        {
                            multiplicity = PropertyMultiplicity.List;
                            relevantType = propInfo.PropertyType.GenericTypeArguments[ 0 ];
                        }
                        else
                        {
                            logger?.Error<string>( "ICollection<> {0} has more than one generic parameter",
                                propInfo.Name );
                            multiplicity = PropertyMultiplicity.Unsupported;
                        }
                    }
                    else
                    {
                        logger?.Error<string>( "ICollection {0} is not generic", propInfo.Name );
                        multiplicity = PropertyMultiplicity.Unsupported;
                    }
                }
                else
                {
                    var numIndexParams = propInfo.GetMethod?.GetParameters().Length;

                    if( numIndexParams == 0 )
                        multiplicity = typeof(string).IsAssignableFrom( propInfo.PropertyType )
                            ? PropertyMultiplicity.String
                            : PropertyMultiplicity.SingleValue;
                    else
                    {
                        logger?.Error<string>( "Property {0} is indexed but in an unsupported way", propInfo.Name );
                        multiplicity = PropertyMultiplicity.Unsupported;
                    }
                }
            }

            var retVal = new TargetableProperty( propInfo, keyComp )
            {
                Multiplicity = multiplicity,
                IsCreateable = relevantType.HasPublicParameterlessConstructor(),
                IsDefined = propInfo.GetValue( container ) != null,
                IsPubliclyReadWrite = propInfo.IsPublicReadWrite( logger ),
                Path = pathToContainer.ToList()
            };

            return retVal;
        }

        private readonly StringComparison _keyComp;

        private TargetableProperty( PropertyInfo propertyInfo, StringComparison keyComp )
        {
            PropertyInfo = propertyInfo;
            _keyComp = keyComp;
        }

        public PropertyInfo PropertyInfo { get; }
        
        public bool IsCreateable { get; private set; }
        public bool IsDefined { get; private set; }
        public bool IsPubliclyReadWrite { get; private set; }
        public PropertyMultiplicity Multiplicity { get; private set; }

        public bool IsTargetable => ( IsCreateable || IsDefined )
                                    && IsPubliclyReadWrite
                                    && ( Multiplicity != PropertyMultiplicity.Unsupported );

        public IOption? BoundOption { get; set; }

        public List<PropertyInfo> Path { get; private set; }

        public MappingResults MapParseResult( 
            IBindingTarget bindingTarget, 
            ParseResults parseResults, 
            IJ4JLogger? logger = null )
        {
            // validate parameters and state
            if( BoundOption == null )
            {
                logger?.Error<string>( "Trying to map parsing results to unbound property {0}", PropertyInfo.Name );
                return MappingResults.Unbound;
            }

            if( Multiplicity == PropertyMultiplicity.Unsupported )
            {
                logger?.Error<string>("Property {0} has an unsupported Multiplicity", PropertyInfo.Name);
                return MappingResults.UnsupportedMultiplicity;
            }

            // see if our BoundOption's keys match a key in the parse results so we can retrieve a
            // specific IParseResult
            var parseResult = parseResults
                .FirstOrDefault(pr =>
                    BoundOption.Keys.Any(k => string.Equals(k, pr.Key, _keyComp)));

            var retVal = MappingResults.Success;
            string optionKey;

            // start by setting the value we're going to set on our bound property to 
            // whatever default was specified for our BoundOption
            object propValue = BoundOption.DefaultValue;;

            if ( parseResult == null )
            {
                // set a return flag if there's no matching IParseResult and pick a default key
                // value (needed for displaying context-sensitive help)
                logger?.Error<string>( "No matching argument keys for property {0}", PropertyInfo.Name );

                retVal |= MappingResults.NoKeyFound;

                optionKey = BoundOption.Keys.First();
            }
            else
            {
                // store the option key that we matched on for later use in displaying context-sensitive help
                optionKey = parseResult.Key;

                // the particular Option conversion method we call depends on whether or not we're binding to 
                // a collection/array or a single value
                switch (Multiplicity)
                {
                    case PropertyMultiplicity.Array:
                    case PropertyMultiplicity.List:
                        if( BoundOption.ConvertList( bindingTarget, parseResult, out var collectionResult ) !=
                            TextConversionResult.Okay )
                        {
                            logger?.Error<string, string>( "Couldn't parse {0} to property {1}",
                                parseResult.ParametersToText(), PropertyInfo.Name );

                            // set a flag to show the error. Note that the default value is still 
                            // the value we'll use to set our target property
                            retVal |= MappingResults.ConversionFailed;
                        }
                        else
                        {
                            // if conversion succeeded, store the result, converting it to a
                            // simple array if necessary
                            if( Multiplicity == PropertyMultiplicity.Array )
                                propValue = collectionResult.ToArray();
                            else propValue = collectionResult;
                        }

                        break;

                    case PropertyMultiplicity.SingleValue:
                    case PropertyMultiplicity.String:
                        if (BoundOption.Convert(bindingTarget, parseResult, out var singleResult) != TextConversionResult.Okay)
                        {
                            logger?.Error<string, string>("Couldn't parse {0} to property {1}",
                                parseResult.ParametersToText(), PropertyInfo.Name);

                            // set a flag to show the error. Note that the default value is still 
                            // the value we'll use to set our target property
                            retVal |= MappingResults.ConversionFailed;
                        }
                        else propValue = singleResult;

                        break;
                }
            }

            if (!BoundOption.Validate(bindingTarget, optionKey, propValue))
            {
                // revert to our default value (which we presume is valid but don't actually know
                // or care)
                propValue = BoundOption.DefaultValue;

                // set a flag to record the validation failure
                retVal |= MappingResults.ValidationFailed;
            }

            // navigate down to our immediate container,
            // initializing stuff along the way...
            var container = bindingTarget.GetValue();

            for( var idx = 0; idx < Path.Count; idx++  )
            {
                var value = Path[idx].GetValue( container );

                if( idx < ( Path.Count - 1 ) )
                {
                    value ??= Activator.CreateInstance( Path[ idx ].PropertyType );

                    container = value!;
                }
            }

            // finally, set the target property's value
            PropertyInfo.SetValue(container, propValue);

            return retVal;
        }
    }
}