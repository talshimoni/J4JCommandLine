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

using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using J4JSoftware.Logging;

namespace J4JSoftware.Configuration.CommandLine
{
    public abstract class DisplayHelp : IDisplayHelp
    {
        protected DisplayHelp( 
            IJ4JLogger? logger 
            )
        {
            Logger = logger;
            Logger?.SetLoggedType(GetType());
        }

        protected IJ4JLogger? Logger { get; }
        protected  IMasterTextCollection? MasterText { get; private set; }

        public bool IsValid => MasterText != null;

        public void Initialize( IMasterTextCollection masterText )
            => MasterText = masterText;

        public abstract void ProcessOptions( IOptionCollection options );

        protected List<string> GetKeys( IOption option )
        {
            var retVal = new List<string>();

            if( MasterText == null )
            {
                Logger?.Error("IDisplayHelp is not initialized, help display not available");
                return retVal;
            }

            foreach( var prefix in MasterText[ TextUsageType.Prefix ] )
            {
                foreach( var key in option.Keys )
                {
                    retVal.Add( $"{prefix}{key}" );
                }
            }

            return retVal;
        }

        protected string GetStyleText( IOption option )
        {
            var reqdText = option.Required ? "must" : "can";

            switch( option.Style )
            {
                case OptionStyle.Collection:
                    var sb = new StringBuilder();

                    sb.Append( option.MaxValues == int.MaxValue ? $"one to {option.MaxValues:n0}" : "one or more" );
                    sb.Append( $" values {reqdText} be specified" );

                    return sb.ToString();

                case OptionStyle.ConcatenatedSingleValue:
                    return $"one or more related values (e.g., flagged enums) {reqdText} be specified";

                case OptionStyle.SingleValued:
                    return $"a single value {reqdText} be specified";

                case OptionStyle.Switch:
                    return "any value specified will be ignored";
            }

            throw new InvalidEnumArgumentException( $"Unsupported {typeof(OptionStyle)} '{option.Style}'" );
        }
    }
}