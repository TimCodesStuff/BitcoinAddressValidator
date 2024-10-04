using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitcoinAddressValidator
{
    public class StringIncrementer
    {
        //private static readonly char[] CharSet = "abcdefghijklmnopqrstuvwxyz-_ ".ToCharArray();
        //private static readonly char[] CharSet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ -_!@#$%^&*()".ToCharArray();
        private static readonly char[] CharSet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz".ToCharArray();
        //private static readonly char[] CharSet = "abc ".ToCharArray(); //456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ -_!@#$%^&*()".ToCharArray();

        // Increment the string with wraparound behavior for the new character set
        public static string IncrementString(string input)
        {
            char[] chars = input.ToCharArray();
            bool carry = true;

            for (int i = chars.Length - 1; i >= 0 && carry; i--)
            {
                int index = Array.IndexOf(CharSet, chars[i]);

                if (index == -1)
                {
                    throw new InvalidOperationException("Invalid character in string.");
                }

                if (index == CharSet.Length - 1)
                {
                    chars[i] = CharSet[0]; // wrap around
                }
                else
                {
                    chars[i] = CharSet[index + 1];
                    carry = false;
                }
            }

            if (carry)
            {
                return CharSet[0] + new string(chars);
            }

            return new string(chars);
        }
    }


}
