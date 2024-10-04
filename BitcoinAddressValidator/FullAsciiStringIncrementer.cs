using System;

public class FullASCIIStringIncrementer
{
    // Increment the string with wraparound behavior for the full ASCII character set
    public static string IncrementString(string input)
    {
        char[] chars = input.ToCharArray();
        bool carry = true;

        for (int i = chars.Length - 1; i >= 0 && carry; i--)
        {
            if (chars[i] == 255) // Last ASCII value
            {
                chars[i] = (char)0; // Wrap around to the first ASCII value
            }
            else
            {
                chars[i]++; // Simply increment the ASCII value
                carry = false;
            }
        }

        if (carry)
        {
            return (char)0 + new string(chars); // Prepend the lowest ASCII character
        }

        return new string(chars);
    }
}
