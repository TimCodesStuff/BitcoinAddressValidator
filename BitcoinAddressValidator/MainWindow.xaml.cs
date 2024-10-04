
using System.Windows;
using System.Windows.Media;
using NBitcoin;
using System.Net.Http;
using Newtonsoft.Json;
using System.Text;
using System.IO;
using NBitcoin.DataEncoders;
using System.Security.Cryptography;


namespace BitcoinAddressValidator
{
    public partial class MainWindow : Window
    {
        // Dictionary for storing addresses for O(1) lookup
        private Dictionary<string, BitcoinAddressRecord> addressDictionary = new Dictionary<string, BitcoinAddressRecord>();
        private AddressCrawler addressCrawler;

        public MainWindow()
        {
            InitializeComponent();

            // Ensure the SQLite database is created
            using (var context = new BitcoinAddressContext())
            {
                context.InitializeDatabase();
                LoadAddressesFromDatabase(); // Load addresses from DB into the ListView
                                             // Initialize the address crawler with the address dictionary
                addressCrawler = new AddressCrawler(addressDictionary);
            }
        }

        // Method to update the UI with crawl progress from the background thread
    /*    private Task UpdateCrawlResultsAsync(string currentString)
        {
            return Dispatcher.InvokeAsync(() =>
            {
                CrawlResults.AppendText($"Checked string: {currentString}\n");
                CrawlResults.ScrollToEnd(); // Ensure the latest result is visible
            }).Task;
        }*/


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
                await Task.Delay(2000);
                string resultMessage = await ProcessAddressAsync(address.Trim());
                ResultList.Items.Add(resultMessage);
            }

            // Refresh the ListView in the second tab to show newly added addresses
            LoadAddressesFromDatabase();
        }

        // Process each address: Validate, classify, and check balance
        private async Task<string> ProcessAddressAsync(string address)
        {
            // Check if the address already exists in the dictionary (O(1) lookup)
            if (addressDictionary.ContainsKey(address))
            {
                var existingAddress = addressDictionary[address];
                //return $"Address: {address} - Already in database. Type: {existingAddress.Type}, Balance: {existingAddress.Balance} BTC";
                return $"Address: {address} - Already in database.";// Type: {existingAddress.Type}, Balance: {existingAddress.Balance} BTC";
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
            if (balance > 0)
            {
                // Add to database and dictionary only if balance is greater than 0
                await AddAddressToDatabase(address, addressType, balance);
                return $"Address: {address} - Type: {addressType}, Balance: {balance} BTC - Added to database";
            }
            else if (balance == 0)
            {
                return $"Address: {address} - Type: {addressType}, Balance: 0 BTC - Not added to database";
            }
            else
            {
                return $"Address: {address} - Failed to retrieve balance.";
            }
        }

        // Add the Bitcoin address to the SQLite database and the dictionary
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

                // Add to dictionary for O(1) lookup
                //addressDictionary[address] = addressRecord;
                addressDictionary[address] = null;
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



        // Load addresses from the SQLite database and store them in the dictionary for O(1) lookup
        private void LoadAddressesFromDatabase()
        {
            using (var context = new BitcoinAddressContext())
            {
                // Fetch the addresses from the database
                var addresses = context.Addresses.ToList();

                // Populate the dictionary for O(1) lookup
                addressDictionary.Clear();
                foreach (var addressRecord in addresses)
                {
                    if (!addressDictionary.ContainsKey(addressRecord.Address))
                    {
                        addressDictionary[addressRecord.Address] = addressRecord;
                    }
                }

                // Set the DataGrid's item source to the list of addresses
                AddressesDataGrid.ItemsSource = addresses;
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

        // Event handler for the "Remove Selected Address" button
        private void RemoveSelectedAddress_Click(object sender, RoutedEventArgs e)
        {
            if (AddressesDataGrid.SelectedItem is BitcoinAddressRecord selectedAddress)
            {
                var result = MessageBox.Show($"Are you sure you want to remove the address: {selectedAddress.Address}?", "Confirm Delete", MessageBoxButton.YesNo);

                if (result == MessageBoxResult.Yes)
                {
                    // Remove the selected address from the database
                    DeleteAddressFromDb(selectedAddress.Id);

                    // Refresh the DataGrid
                    LoadAddressesFromDatabase();
                }
            }
        }

        // Method to delete the selected address from the SQLite database
        private void DeleteAddressFromDb(int id)
        {
            using (var context = new BitcoinAddressContext())
            {
                // Find the address record by its Id
                var addressRecord = context.Addresses.FirstOrDefault(a => a.Id == id);

                if (addressRecord != null)
                {
                    // Remove the address record from the database
                    context.Addresses.Remove(addressRecord);
                    context.SaveChanges(); // Save changes to commit the deletion
                }
            }
        }

        // Check if the given address exists in the dictionary (O(1) lookup)
        private bool AddressExistsInDatabase(string address)
        {
            return addressDictionary.ContainsKey(address);
        }

        // Event handler for generating addresses from the input string
        private void GenerateAddresses_Click(object sender, RoutedEventArgs e)
        {
            // Get the input string
            string inputString = StringInput.Text;

            if (string.IsNullOrWhiteSpace(inputString))
            {
                MessageBox.Show("Please enter a valid string.");
                return;
            }

            // Generate SHA-256 hash
            string privateKeyHex = ComputeSHA256Hash(inputString);
            var key = new Key(Encoders.Hex.DecodeData(privateKeyHex));

            // Generate P2PKH (Legacy) address
            string p2pkhAddress = key.PubKey.GetAddress(ScriptPubKeyType.Legacy, Network.Main).ToString();

            // Generate P2SH (P2WPKH wrapped in P2SH) address
            string p2shAddress = key.PubKey.WitHash.ScriptPubKey.Hash.GetAddress(Network.Main).ToString();

            // Generate Segwit (Bech32) address
            string segwitAddress = key.PubKey.WitHash.GetAddress(Network.Main).ToString();

            // Check if the addresses are in the dictionary (O(1) lookup)
            bool p2pkhExists = AddressExistsInDatabase(p2pkhAddress);
            bool p2shExists = AddressExistsInDatabase(p2shAddress);
            bool segwitExists = AddressExistsInDatabase(segwitAddress);

            // Set the values in the TextBoxes
            P2PKHTextBox.Text = p2pkhAddress;
            P2SHTextBox.Text = p2shAddress;
            SegwitTextBox.Text = segwitAddress;

            // Set the status for whether the addresses exist in the database
            P2PKHStatus.Text = p2pkhExists ? "Found in DB" : "Not Found in DB";
            P2SHStatus.Text = p2shExists ? "Found in DB" : "Not Found in DB";
            SegwitStatus.Text = segwitExists ? "Found in DB" : "Not Found in DB";
        }

        // Copy button for P2PKH address
        private void CopyP2PKH_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(P2PKHTextBox.Text);
        }

        // Copy button for P2SH address
        private void CopyP2SH_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(P2SHTextBox.Text);
        }

        // Copy button for Segwit address
        private void CopySegwit_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(SegwitTextBox.Text);
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

        private CancellationTokenSource _cancellationTokenSource;
        private long _stringsCheckedCount;  // Counter to track the number of strings checked

        private async void StartCrawling_Click(object sender, RoutedEventArgs e)
        {
            string startString = CrawlStringInput.Text;

            if (string.IsNullOrWhiteSpace(startString))
            {
                MessageBox.Show("Please enter a valid starting string.");
                return;
            }

            // Initialize the CancellationTokenSource
            _cancellationTokenSource = new CancellationTokenSource();
            _stringsCheckedCount = 0;
            UpdateStringsCheckedCount();  // Initialize the UI

            // Disable the UI elements while crawling
            CrawlStringInput.IsEnabled = false;
            CrawlResults.Clear(); // Clear previous results

            // Run the crawling logic in a background task
            await Task.Run(async () =>
            {
                try
                {
                    // Start the crawling process
                    string result = await addressCrawler.CrawlAndFindMatch(startString, UpdateCrawlResultsAsync, _cancellationTokenSource.Token);

                    // After completion, update the UI on the main thread
                    Dispatcher.Invoke(() =>
                    {
                        CrawlResults.Text += $"\n{result}";
                        //CrawlStringInput.IsEnabled = true; // Re-enable the input field
                    });
                }
                catch (OperationCanceledException)
                {
                    Dispatcher.Invoke(() =>
                    {
                        CrawlResults.AppendText("\nCrawling stopped by the user.\n");
                        CrawlStringInput.IsEnabled = true;
                    });
                }
            });
        }

        // Method to update the strings checked count on the UI
        private void UpdateStringsCheckedCount()
        {
            Dispatcher.Invoke(() =>
            {
                StringsCheckedCount.Text = _stringsCheckedCount.ToString();
            });
        }

        // Method to batch update the UI every 20 checks
        private Task UpdateCrawlResultsAsync(string currentString)
        {
            // Increment the counter
            _stringsCheckedCount++;
            UpdateStringsCheckedCount();

            // Update the crawling results
            return Dispatcher.InvokeAsync(() =>
            {     
                
                if (_stringsCheckedCount % 200 == 0)
                {
                    if (_stringsCheckedCount % 4000 == 0)
                    {
                        CrawlResults.Clear();
                    }
                    else
                    {
                        CrawlResults.AppendText($"Checked string: {currentString}\n");
                    }
                }

                //CrawlResults.ScrollToEnd(); // Ensure the latest result is visible
                
            }).Task;
        }


        // Handle the "Stop" button
        private void StopCrawling_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource?.Cancel(); // Trigger cancellation
        }


    }

}
