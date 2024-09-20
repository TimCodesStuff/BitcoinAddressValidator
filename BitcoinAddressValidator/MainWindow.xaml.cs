using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using NBitcoin;
using System.Net.Http;
using Newtonsoft.Json;
using System.Text;
using System.IO;

namespace BitcoinAddressValidator
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Ensure the SQLite database is created
            using (var context = new BitcoinAddressContext())
            {
                context.InitializeDatabase();
                LoadAddressesFromDatabase(); // Load addresses from DB into the ListView
            }
        }

        // Placeholder behavior for multi-line TextBox: Remove placeholder text when the TextBox gains focus
        private void AddressInput_GotFocus(object sender, RoutedEventArgs e)
        {
            if (AddressInput.Text == "Enter Bitcoin Addresses (one per line)")
            {
                AddressInput.Text = "";
                AddressInput.Foreground = new SolidColorBrush(Colors.Black); // Change text color to normal
            }
        }

        // Restore placeholder behavior when the TextBox loses focus
        private void AddressInput_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(AddressInput.Text))
            {
                AddressInput.Text = "Enter Bitcoin Addresses (one per line)";
                AddressInput.Foreground = new SolidColorBrush(Colors.Gray); // Set text color to placeholder style
            }
        }

        // Handle button click: Validate and add a group of Bitcoin addresses
        private async void ValidateButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear previous results
            ResultList.Items.Clear();

            // Split the input into multiple addresses (one per line)
            string[] addresses = AddressInput.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string address in addresses)
            {
                string resultMessage = await ProcessAddressAsync(address.Trim());
                ResultList.Items.Add(resultMessage);
            }

            // Refresh the ListView in the second tab to show newly added addresses
            LoadAddressesFromDatabase();
        }

        // Process each address: Validate, classify, and check balance
        private async Task<string> ProcessAddressAsync(string address)
        {
            using (var context = new BitcoinAddressContext())
            {
                // Check if the address already exists in the database
                var existingAddress = context.Addresses.FirstOrDefault(a => a.Address == address);
                if (existingAddress != null)
                {
                    return $"Address: {address} - Already in database. Type: {existingAddress.Type}, Balance: {existingAddress.Balance} BTC";
                }
            }

            // Validate Bitcoin address
            if (!ValidateBitcoinAddress(address))
            {
                return $"Address: {address} - Invalid Bitcoin address.";
            }

            // Classify Bitcoin address
            string addressType = ClassifyAddress(address);

            // Get balance via API call
            decimal balance = await GetBitcoinBalance(address);
            if (balance >= 0)
            {
                // Add to database if valid
                await AddAddressToDatabase(address, addressType, balance);
                return $"Address: {address} - Type: {addressType}, Balance: {balance} BTC";
            }
            else
            {
                return $"Address: {address} - Failed to retrieve balance.";
            }
        }

        // Validate the Bitcoin address format using NBitcoin
        private bool ValidateBitcoinAddress(string address)
        {
            try
            {
                BitcoinAddress bitcoinAddress = BitcoinAddress.Create(address, Network.Main);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Classify the address type (P2PKH, P2SH, or Segwit)
        private string ClassifyAddress(string address)
        {
            if (address.StartsWith("1"))
                return "P2PKH";
            else if (address.StartsWith("3"))
                return "P2SH";
            else if (address.StartsWith("bc1"))
                return "Segwit";
            else
                return "Unknown";
        }

        // Get the Bitcoin balance from an external API
        private async Task<decimal> GetBitcoinBalance(string address)
        {
            using (var client = new HttpClient())
            {
                string apiUrl = $"https://blockchain.info/q/addressbalance/{address}";
                var response = await client.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();

                    // Try to parse the response as a long (satoshis)
                    if (long.TryParse(responseContent, out long satoshiBalance))
                    {
                        // Convert satoshis to bitcoin
                        return satoshiBalance / 100000000m; // Convert from Satoshis to Bitcoin
                    }
                    else
                    {
                        MessageBox.Show("Failed to parse balance.");
                        return -1;
                    }
                }
                else
                {
                    MessageBox.Show("Failed to retrieve balance from the API.");
                    return -1;
                }
            }
        }

        // Add the Bitcoin address to the SQLite database
        private async Task AddAddressToDatabase(string address, string type, decimal balance)
        {
            using (var context = new BitcoinAddressContext())
            {
                var addressRecord = new BitcoinAddressRecord
                {
                    Address = address,
                    Type = type,
                    Balance = balance
                };
                context.Addresses.Add(addressRecord);
                await context.SaveChangesAsync();
            }
        }

        // Load addresses from the SQLite database and display them in the ListView
        private void LoadAddressesFromDatabase()
        {
            using (var context = new BitcoinAddressContext())
            {
                // Fetch the addresses from the database
                var addresses = context.Addresses.ToList();

                // Clear the existing items
                AddressListView.Items.Clear();

                // Add each address to the ListView
                foreach (var addressRecord in addresses)
                {
                    AddressListView.Items.Add(addressRecord);
                }
            }
        }

        // Method to export addresses to JSON
        private void ExportToJson_Click(object sender, RoutedEventArgs e)
        {
            using (var context = new BitcoinAddressContext())
            {
                var addresses = context.Addresses.ToList();

                // Serialize the addresses to JSON
                string json = JsonConvert.SerializeObject(addresses, Formatting.Indented);

                // Save the JSON to a file
                File.WriteAllText("addresses_export.json", json);

                MessageBox.Show("Addresses exported to addresses_export.json");
            }
        }

        // Method to export addresses to CSV
        private void ExportToCsv_Click(object sender, RoutedEventArgs e)
        {
            using (var context = new BitcoinAddressContext())
            {
                var addresses = context.Addresses.ToList();

                StringBuilder csv = new StringBuilder();
                csv.AppendLine("Address,Type,Balance"); // CSV headers

                foreach (var address in addresses)
                {
                    csv.AppendLine($"{address.Address},{address.Type},{address.Balance}");
                }

                // Save the CSV to a file
                File.WriteAllText("addresses_export.csv", csv.ToString());

                MessageBox.Show("Addresses exported to addresses_export.csv");
            }
        }


    }
}
