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
using System.Text;
using J4JSoftware.Logging;

#pragma warning disable 8618

namespace J4JSoftware.Configuration.CommandLine
{
    public class ConsolidateQuotedText : ICleanupTokens
    {
        private readonly IJ4JLogger? _logger;
        private readonly StringComparison _textComparison;

        public ConsolidateQuotedText(
            StringComparison textComparison,
            IJ4JLogger? logger )
        {
            _textComparison = textComparison;
            _logger = logger;
        }

        public void Process( List<Token> tokens )
        {
            var startIdx = 0;
            QuoterPair? curPair;

            // loop through all QuoterPairs (i.e., pairs of quoter tokens defining
            // a quoted sequence
            while( ( curPair = GetQuoterPair( tokens, startIdx ) )?.Start != null )
            {
                // a quoting pair without an end point is an error. delete the
                // "quoted" tokens
                if( curPair.End == null )
                {
                    _logger?.Error( "Unclosed quoter encountered, command line truncated at token #{0}",
                        curPair.Start.Index );

                    tokens.RemoveFrom( curPair.Start.Index );

                    return;
                }

                // consolidate the intervening tokens into a single text token
                var sb = new StringBuilder();
                var toRemove = new List<int>();

                for( var idx = curPair.Start.Index + 1; idx < curPair.End.Index; idx++ )
                {
                    sb.Append( tokens[ idx ]!.Text );

                    // keep track of the tokens we'll need to delete
                    toRemove.Add( idx );
                }

                // add the closing quoter to the list of tokens to delete
                toRemove.Add( curPair.End.Index );

                // remove all the quoted tokens, including the closing quoter,
                // but leave the opening quoter in place so we can replace it
                // with the consolidated text token.
                tokens.RemoveRange( toRemove );

                tokens[ curPair.Start.Index ] = new Token( LexicalType.Text, sb.ToString() );

                // resume the search for QuotedPairs at the next token
                startIdx = curPair.Start.Index + 1;
            }
        }

        private QuoterPair? GetQuoterPair( List<Token> tokens, int startIdx = 0 )
        {
            var firstQuoter = tokens.Select( ( t, i ) => new QuoterLocation
                {
                    Token = t,
                    Index = i
                } )
                .FirstOrDefault( t => t.Index >= startIdx && t.Token.Type == LexicalType.Quoter );

            if( firstQuoter == null )
                return null;

            return new QuoterPair
            {
                Start = firstQuoter,
                End = tokens.Select( ( t, i ) => new QuoterLocation
                    {
                        Token = t,
                        Index = i
                    } )
                    .FirstOrDefault( t =>
                        t.Index > firstQuoter.Index
                        && t.Token.Type == LexicalType.Quoter
                        && t.Token.Text.Equals( firstQuoter.Token.Text, _textComparison ) )
            };
        }

        private class QuoterLocation
        {
            public Token Token { get; set; }
            public int Index { get; set; }
        }

        private class QuoterPair
        {
            public QuoterLocation? Start { get; set; }
            public QuoterLocation? End { get; set; }
        }
    }
}