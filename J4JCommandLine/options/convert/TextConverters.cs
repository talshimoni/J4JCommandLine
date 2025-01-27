﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using J4JSoftware.Logging;

namespace J4JSoftware.Configuration.CommandLine
{
    public class TextConverters : ITextConverters
    {
        private readonly Dictionary<Type, ITextToValue> _converters = new();
        private readonly BuiltInConverters _builtInConv;
        private readonly List<BuiltInConverter>? _builtInTargets;
        private readonly IJ4JLogger? _logger;

        private record BuiltInConverter(Type ReturnType, MethodInfo MethodInfo);

        private static List<ITextToValue> GetBuiltInConverters(IJ4JLogger? logger)
        {
            var retVal = new List<ITextToValue>();

            foreach (var builtInConverter in GetBuiltInTargetTypes())
            {
                var builtInType = typeof(BuiltInTextToValue<>).MakeGenericType(builtInConverter.ReturnType);

                retVal.Add((ITextToValue)Activator.CreateInstance(
                        builtInType,
                        new object?[] { builtInConverter.MethodInfo, logger })!
                );
            }

            return retVal;
        }

        private static List<BuiltInConverter> GetBuiltInTargetTypes() =>
            typeof(Convert)
                .GetMethods(BindingFlags.Static | BindingFlags.Public)
                .Where(m =>
                {
                    var parameters = m.GetParameters();

                    return parameters.Length == 1 && !typeof(string).IsAssignableFrom(parameters[0].ParameterType);
                })
                .Select(x => new BuiltInConverter(x.ReturnType, x))
                .ToList();

        public TextConverters(
            BuiltInConverters builtInConv = BuiltInConverters.AddDynamically,
            IJ4JLogger? logger = null,
            params ITextToValue[] converters)
        {
            _logger = logger;
            _logger?.SetLoggedType( GetType() );

            AddConverters( converters );
            _builtInConv = builtInConv;

            switch (builtInConv)
            {
                case BuiltInConverters.AddAtInitialization:
                    AddConverters(GetBuiltInConverters(logger));
                    break;

                case BuiltInConverters.AddDynamically:
                    _builtInTargets = GetBuiltInTargetTypes();
                    break;
            }
        }

        public IEnumerable<Type> Keys => _converters.Keys;
        public IEnumerable<ITextToValue> Values => _converters.Values;
        public int Count => _converters.Count;

        public bool ContainsKey(Type key) => _converters.ContainsKey(key);

        public bool AddConverter(ITextToValue converter, bool replaceExisting = false)
        {
            if (_converters.ContainsKey(converter.TargetType))
            {
                if (!replaceExisting)
                {
                    _logger?.Error("There is already a converter defined for {0}", converter.TargetType);
                    return false;
                }

                _converters[converter.TargetType] = converter;

                return true;
            }

            _converters.Add(converter.TargetType, converter);

            return true;
        }

        public bool AddConverters(IEnumerable<ITextToValue> converters, bool replaceExisting = false)
        {
            var retVal = true;

            foreach (var converter in converters)
            {
                retVal &= AddConverter(converter, replaceExisting);
            }

            return retVal;
        }

        public bool CanConvert( Type toCheck )
        {
            // we can convert any type for which we have a converter, plus lists and arrays of those types
            if( toCheck.IsArray )
            {
                var elementType = toCheck.GetElementType();
                return elementType != null && CanConvertSimple( elementType );
            }

            if( toCheck.IsGenericType )
            {
                var genArgs = toCheck.GetGenericArguments();
                if( genArgs.Length != 1 )
                    return false;

                if( !CanConvertSimple( genArgs[ 0 ] ) )
                    return false;

                return ( typeof(List<>).MakeGenericType( genArgs[ 0 ] )
                    .IsAssignableFrom( toCheck ) );
            }

            if( CanConvertSimple( toCheck ) )
                return true;

            _logger?.Error( "No ITextToValue converter is defined for {0}", toCheck );

            return false;
        }

        private bool CanConvertSimple(Type simpleType)
        {
            if (simpleType.IsArray || simpleType.IsGenericType)
                return false;

            if (simpleType.IsEnum)
                return true;

            switch (_builtInConv)
            {
                case BuiltInConverters.AddAtInitialization:
                    return _converters.Any(x => x.Value.TargetType == simpleType);

                case BuiltInConverters.AddDynamically:
                    if (_converters.Any(x => x.Value.TargetType == simpleType))
                        return true;

                    break;

                case BuiltInConverters.DoNotAdd:
                    return false;
            }

            // try to add a built-in converter dynamically
            var builtInConverter = _builtInTargets!.FirstOrDefault(x => x.ReturnType == simpleType);
            if (builtInConverter == null)
                return false;

            var builtInType = typeof(BuiltInTextToValue<>).MakeGenericType(builtInConverter.ReturnType);

            _converters.Add(simpleType,
                (ITextToValue)Activator.CreateInstance(
                    builtInType,
                    new object?[] { builtInConverter.MethodInfo, _logger }
                )!
            );

            return true;
        }

        public bool Convert(Type targetType, IEnumerable<string> textValues, out object? result)
        {
            result = null;

            var converter = _converters.Where(x => x.Value.CanConvert(targetType))
                .Select(x => x.Value)
                .FirstOrDefault();

            if (converter != null)
                return converter.Convert(textValues, out result);

            if (targetType.IsEnum)
            {
                var enumConverterType = typeof(TextToEnum<>).MakeGenericType(targetType);
                converter = Activator.CreateInstance(enumConverterType, new object?[] { _logger }) as ITextToValue;
                _converters.Add(targetType, converter!);

                return converter!.Convert(textValues, out result);
            }

            _logger?.Error("Cannot convert text to '{0}'", targetType);

            return false;
        }

        public bool TryGetValue(Type key, out ITextToValue value)
        {
            value = new UndefinedTextToValue();

            if (!_converters.ContainsKey(key))
                return false;

            value = _converters[key];

            return true;
        }

        public ITextToValue this[Type key]
        {
            get
            {
                if (_converters.ContainsKey(key))
                    throw new KeyNotFoundException($"Converter collection does not contain an entry for {key}");

                return _converters[key];
            }
        }

        public IEnumerator<KeyValuePair<Type, ITextToValue>> GetEnumerator()
        {
            foreach( var kvp in _converters )
            {
                yield return kvp;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
