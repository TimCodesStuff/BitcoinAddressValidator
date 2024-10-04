using NBitcoin.DataEncoders;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Net;

namespace BitcoinAddressValidator
{
    public class AddressCrawler
    {
        private Dictionary<string, BitcoinAddressRecord> _addressDictionary;
        private string _filePath = "stringkeys.txt";

        public AddressCrawler(Dictionary<string, BitcoinAddressRecord> addressDictionary)
        {
            _addressDictionary = addressDictionary;
        }

        // Crawl through strings and check for matching addresses, with cancellation support
        public async Task<string> CrawlAndFindMatch(string startString, Func<string, Task> updateCallback, CancellationToken cancellationToken)
         {
             string currentString = startString;
             bool found = false;
             while (!found)
             {
                 // Check for cancellation
                 cancellationToken.ThrowIfCancellationRequested();

                 // Generate SHA-256 hash
                 string privateKeyHex = ComputeSHA256Hash(currentString);
                 var key = new Key(Encoders.Hex.DecodeData(privateKeyHex));

                 // Generate P2PKH, P2SH, Segwit addresses
                 string p2pkhAddress = key.PubKey.GetAddress(ScriptPubKeyType.Legacy, Network.Main).ToString();
                //string p2shAddress = key.PubKey.WitHash.ScriptPubKey.Hash.GetAddress(Network.Main).ToString();
                //string segwitAddress = key.PubKey.WitHash.GetAddress(Network.Main).ToString();
                // Check if any address exists in the dictionary
                if (_addressDictionary.ContainsKey(p2pkhAddress))// || _addressDictionary.ContainsKey(p2shAddress) || _addressDictionary.ContainsKey(segwitAddress))
                 {
                     // Write the matching string to a file
                     await File.AppendAllTextAsync(_filePath, $"{currentString}\n");

                     found = true;
                     return $"Match found! Input string: {currentString}";
                 }

                 // Call the callback to update the UI with progress
                 await updateCallback(currentString);

                 // Increment the string
                 //currentString = FullASCIIStringIncrementer.IncrementString(currentString);
                 currentString = StringIncrementer.IncrementString(currentString);
             }
             return currentString;
         }

        // Compute SHA-256 hash of the input string
        private string ComputeSHA256Hash(string input)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }
        }
    }


}
