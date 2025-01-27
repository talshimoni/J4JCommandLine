﻿#region license

// Copyright 2021 Mark A. Olbert
// 
// This library or program 'J4JCommandLine' is free software: you can redistribute it
// and/or modify it under the terms of the GNU General Public License as
// published by the Free Software Foundation, either version 3 of the License,
// or (at your option) any later version.
// 
// This library or program is distributed in the hope that it will be useful, but
// WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// General Public License for more details.
// 
// You should have received a copy of the GNU General Public License along with
// this library or program.  If not, see <https://www.gnu.org/licenses/>.

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using J4JSoftware.Logging;

namespace J4JSoftware.Configuration.CommandLine
{
    public sealed class TextToValueComparer : IEqualityComparer<ITextToValue>
    {
        public bool Equals(ITextToValue? x, ITextToValue? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (ReferenceEquals(x, null)) return false;
            if (ReferenceEquals(y, null)) return false;
            if (x.GetType() != y.GetType()) return false;
            return x.TargetType.Equals(y.TargetType);
        }

        public int GetHashCode(ITextToValue obj)
        {
            return obj.TargetType.GetHashCode();
        }
    }

    public class UndefinedTextToValue : ITextToValue
    {
        public Type TargetType => typeof(object);
        public bool CanConvert( Type toCheck ) => false;

        public bool Convert( Type targetType, IEnumerable<string> values, out object? result )
        {
            result = null;
            return false;
        }

        public bool Convert<T>( IEnumerable<string> values, out T? result )
        {
            result = default;
            return false;
        }
    }

    public abstract class TextToValue<TBaseType> : ITextToValue
    {
        protected TextToValue( 
            IJ4JLogger? logger
            )
        {
            Logger = logger;
            Logger?.SetLoggedType( GetType() );
        }

        protected abstract bool ConvertTextToValue(string text, out TBaseType? result);

        protected IJ4JLogger? Logger { get; }

        public Type TargetType => typeof(TBaseType);

        public bool CanConvert( Type toCheck ) =>
            toCheck.GetTypeNature() != TypeNature.Unsupported
            && toCheck.IsAssignableFrom( TargetType );

        // targetType must be one of:
        // - TBaseType
        // - TBaseType[]
        // - List<TBaseType>
        // anything else will cause a conversion failure
        public bool Convert( Type targetType, IEnumerable<string> values, out object? result)
        {
            result = default;
            var retVal = false;

            var valueList = values.ToList();

            switch (targetType.GetTypeNature())
            {
                case TypeNature.Simple:
                    if (valueList.Count > 1)
                    {
                        Logger?.Error("Cannot convert multiple text values to a single value of '{0}'",
                            typeof(TBaseType));

                        return false;
                    }

                    retVal = ConvertToSingleValue(valueList.Count == 0 ? null : valueList[0], out var singleResult);
                    result = (object?)singleResult;

                    break;

                case TypeNature.Array:
                    retVal = ConvertToArray(valueList, out var arrayResult);
                    result = (object?)arrayResult;

                    break;

                case TypeNature.List:
                    retVal = ConvertToArray(valueList, out var listResult);
                    result = (object?)listResult;

                    break;
            }

            return retVal;
        }

        // TConvType must be one of:
        // - TBaseType
        // - TBaseType[]
        // - List<TBaseType>
        // anything else will cause a conversion failure
        public bool Convert<TConvType>( IEnumerable<string> values, out TConvType? result )
        {
            result = default;

            if( !Convert( typeof(TConvType), values, out var temp ) )
                return false;

            result = (TConvType?)temp;

            return true;
        }

        private bool ConvertToSingleValue( string? text, out TBaseType? result )
        {
            result = default;

            // null or empty strings return the default value for TBaseType,
            // whatever that may be
            if( string.IsNullOrEmpty( text ) )
                return true;

            return ConvertTextToValue( text, out result );
        }

        private bool ConvertToArray( List<string> values, out TBaseType?[]? result )
        {
            result = null;

            var retVal = new List<TBaseType?>();

            foreach( var value in values )
            {
                if( ConvertToSingleValue( value, out var temp ) )
                    retVal.Add( temp );
                else
                {
                    Logger?.Error( "Could not convert '{0}' to an instance of {1}", value, typeof(TBaseType) );
                    return false;
                }
            }

            result = retVal.ToArray();

            return true;
        }

        private bool ConvertToList( List<string> values, out List<TBaseType?>? result )
        {
            result = null;

            var retVal = new List<TBaseType?>();

            foreach( var value in values )
            {
                if (ConvertToSingleValue(value, out var temp))
                    retVal.Add(temp);
                else
                {
                    Logger?.Error("Could not convert '{0}' to an instance of {1}", value, typeof(TBaseType));
                    return false;
                }
            }

            result = retVal;

            return true;
        }
    }
}