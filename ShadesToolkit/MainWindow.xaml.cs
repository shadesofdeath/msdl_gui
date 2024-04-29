using HandyControl.Themes;
using HtmlAgilityPack;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MessageBox = System.Windows.MessageBox;
namespace MSDL_GUI
{
    public partial class MainWindow
    {
        private const string LangsUrl = "https://www.microsoft.com/en-us/api/controls/contentinclude/html?pageId=cd06bda8-ff9c-4a6e-912a-b92a21f42526&host=www.microsoft.com&segments=software-download%2cwindows11&query=&action=getskuinformationbyproductedition&sdVersion=2";
        private const string DownUrl = "https://www.microsoft.com/en-us/api/controls/contentinclude/html?pageId=cfa9e580-a81e-4a4b-a846-7b21bf4e2e5b&host=www.microsoft.com&segments=software-download%2Cwindows11&query=&action=GetProductDownloadLinksBySku&sdVersion=2";
        private const string SessionUrl = "https://vlscppe.microsoft.com/fp/tags?org_id=y6jn8c31&session_id=";
        private const string SharedSessionGUID = "47cbc254-4a79-4be6-9866-9c625eb20911";
        private const string ApiUrl = "https://api.gravesoft.dev/msdl";

        private HttpClient httpClient;
        private string sessionId;
        private bool sharedSession;
        private string skuId;
        private string productId;
        public MainWindow()
        {
            InitializeComponent();
            Application.Current.Dispatcher.Invoke(() =>
            {
                var theme = Properties.Settings.Default["Theme"].ToString();
                ((App)Application.Current).UpdateTheme(theme == "Dark" ? ApplicationTheme.Dark : ApplicationTheme.Light);
            });
            httpClient = new HttpClient();
            sessionId = Guid.NewGuid().ToString();
            sharedSession = false;
            LoadProducts();

        }
        
        private void LoadProducts()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "MSDL_GUI.data.products.json";

                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                using (StreamReader reader = new StreamReader(stream))
                {
                    string productsJson = reader.ReadToEnd();
                    var products = JsonConvert.DeserializeObject<Dictionary<string, string>>(productsJson);
                    ProductComboBox.ItemsSource = products;
                    ProductComboBox.DisplayMemberPath = "Value";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading products: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ProductComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProductComboBox.SelectedItem == null) return;
            HomeWindow.IsEnabled = false;
            var selectedProduct = (KeyValuePair<string, string>)ProductComboBox.SelectedItem;
            productId = selectedProduct.Key;

            await LoadLanguages(productId);
        }

        private async Task LoadLanguages(string productId)
        {
            try
            {
                string url = LangsUrl + $"&productEditionId={productId}&sessionId={sessionId}";
                string response = await httpClient.GetStringAsync(url);

                var doc = new HtmlDocument();
                doc.LoadHtml(response);

                var options = doc.DocumentNode.SelectNodes("//select[@id='product-languages']/option")
                    .Where(o => o.GetAttributeValue("value", "") != "")
                    .Select(o => new
                    {
                        Text = o.InnerText.Trim(),
                        Value = JsonConvert.DeserializeObject<dynamic>(WebUtility.HtmlDecode(o.GetAttributeValue("value", ""))).id.ToString()
                    })
                    .ToList();

                LanguageComboBox.ItemsSource = options;
                HomeWindow.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading languages: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                HomeWindow.IsEnabled = true;
            }
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (ProductComboBox.SelectedItem == null || LanguageComboBox.SelectedItem == null)
            {
                return;
            }
            
            if (LanguageComboBox.SelectedItem == null) return;
            StatusTextBox.Text = "Getting connection information..";
            ProgressBar.Visibility = Visibility.Visible;
            HomeWindow.IsEnabled = false;
            var selectedLanguage = (dynamic)LanguageComboBox.SelectedItem;
            skuId = selectedLanguage.Value;

            var downloadLinkNodes = await GetDownloadLink(skuId);

            if (downloadLinkNodes != null)
            {
                var window = new DownloadOptionWindow();

                var selectedProduct = (KeyValuePair<string, string>)ProductComboBox.SelectedItem;
                if (selectedProduct.Value.Contains("Windows 11"))
                {
                    window.X32Button.Visibility = Visibility.Collapsed;
                }

                if (window.ShowDialog() == true)
                {
                    string selectedOption = window.SelectedOption;

                    foreach (var downloadLinkNode in downloadLinkNodes)
                    {
                        string downloadLink = downloadLinkNode.GetAttributeValue("href", "");
                        if (selectedOption == "x32" && downloadLink.Contains("x32"))
                        {
                            await DownloadFileAsync(downloadLink);
                            break;
                        }
                        else if (selectedOption == "x64" && downloadLink.Contains("x64"))
                        {
                            HomeWindow.IsEnabled = true;

                            await DownloadFileAsync(downloadLink);

                            break;
                        }
                    }
                }
                else
                {
                    StatusTextBox.Text = "";
                    ProgressBar.Visibility = Visibility.Hidden;
                    HomeWindow.IsEnabled = true;
                }
            }
            else
            {
                StatusTextBox.Text = "";
                ProgressBar.Visibility = Visibility.Hidden;
                LanguageComboBox.IsEnabled = false;
                ProductComboBox.IsEnabled = false;
                DownloadButton.IsEnabled = false;
            }
        }


        private async Task<HtmlNodeCollection> GetDownloadLink(string skuId)
        {
            try
            {
                string url = DownUrl + $"&skuId={skuId}&sessionId={sessionId}";
                string response = await TryGetResponse(url);

                if (response == null || response.Contains("Error"))
                {
                    url = DownUrl + $"&skuId={skuId}&sessionId={SharedSessionGUID}";
                    response = await TryGetResponse(url);
                }

                if (response == null || response.Contains("Error"))
                {
                    url = ApiUrl + "/proxy?product_id=" + productId + "&sku_id=" + skuId;
                    response = await TryGetResponse(url);
                }

                if (response != null && !response.Contains("Error"))
                {
                    var doc = new HtmlDocument();
                    doc.LoadHtml(response);

                    var downloadLinkNodes = doc.DocumentNode.SelectNodes("//a[contains(@href, '.iso')]");
                    return downloadLinkNodes;
                }
                else
                {
                    StatusTextBox.Text = "Error receiving connection!";
                    return null;
                }
            }
            catch (Exception)
            {
                StatusTextBox.Text = "Error receiving connection! ";
                return null;
            }
        }


        private async Task DownloadFileAsync(string downloadLink)
        {
            try
            {
                var saveFileDialog = new SaveFileDialog();
                var uri = new Uri(downloadLink);
                string fileName = Path.GetFileName(uri.LocalPath);
                saveFileDialog.FileName = fileName;
                saveFileDialog.Filter = "ISO files (*.iso)|*.iso";
                saveFileDialog.DefaultExt = ".iso";

                if (saveFileDialog.ShowDialog() == true)
                {
                    string fullPath = saveFileDialog.FileName;

                    using (var webClient = new WebClient())
                    {
                        webClient.DownloadProgressChanged += (s, e) =>
                        {
                            ProgressBar.Value = e.ProgressPercentage;
                            double bytesIn = double.Parse(e.BytesReceived.ToString());
                            double totalBytes = double.Parse(e.TotalBytesToReceive.ToString());
                            double percentage = bytesIn / totalBytes * 100;

                            if (bytesIn > 1024 * 1024 * 1024)
                            {
                                IsoSize.Text = $"( {Math.Round(bytesIn / 1024 / 1024 / 1024, 2)} GB / {Math.Round(totalBytes / 1024 / 1024 / 1024, 2)} GB )";
                            }
                            else
                            {
                                IsoSize.Text = $"( {Math.Round(bytesIn / 1024 / 1024, 2)} MB / {Math.Round(totalBytes / 1024 / 1024 / 1024, 2)} GB )";
                            }
                        };


                        webClient.DownloadFileCompleted += (s, e) =>
                        {
                            if (e.Error == null)
                            {
                                MessageBox.Show("ISO downloaded successfully.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                                ProgressBar.Visibility = Visibility.Hidden;
                                LanguageComboBox.IsEnabled = true;
                                ProductComboBox.IsEnabled = true;
                                DownloadButton.IsEnabled = true;
                                StatusTextBox.Text = "";
                            }
                            else
                            {
                                MessageBox.Show($"Error downloading ISO: {e.Error.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                ProgressBar.Visibility = Visibility.Hidden;
                                LanguageComboBox.IsEnabled = true;
                                ProductComboBox.IsEnabled = true;
                                DownloadButton.IsEnabled = true;
                                StatusTextBox.Text = "";
                            }
                        };
                        StatusTextBox.Text = "Download Started..";
                        HomeWindow.IsEnabled = true;
                        LanguageComboBox.IsEnabled = false;
                        ProductComboBox.IsEnabled = false;
                        DownloadButton.IsEnabled = false;
                        ProgressBar.Visibility = Visibility.Visible;
                        await webClient.DownloadFileTaskAsync(new Uri(downloadLink), fullPath);
                    }
                }
                else
                {
                    StatusTextBox.Text = "";
                    ProgressBar.Visibility = Visibility.Hidden;
                    LanguageComboBox.IsEnabled = true;
                    ProductComboBox.IsEnabled = true;
                    DownloadButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error downloading ISO:: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusTextBox.Text = "Error Occurred!";
                StatusTextBox.Text = "";
                ProgressBar.Visibility = Visibility.Hidden;
                LanguageComboBox.IsEnabled = true;
                ProductComboBox.IsEnabled = true;
                DownloadButton.IsEnabled = true;
            }
        }


        private void Client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            ProgressBar.Value = e.ProgressPercentage;
        }


        private async Task<string> TryGetResponse(string url)
        {
            try
            {
                string response = await httpClient.GetStringAsync(url);
                if (response.Contains("Error"))
                {
                    url = ApiUrl + "proxy" + "?product_id=" + productId + "&sku_id=" + skuId;
                    response = await httpClient.GetStringAsync(url);
                }
                if (response.Contains("Error"))
                {
                    url = url.Replace(sessionId, SharedSessionGUID);
                    response = await httpClient.GetStringAsync(url);
                }
                
                return response;
            }
            catch (Exception)
            {
                return null;
            }
        }

        #region Change Theme
        private void ButtonConfig_OnClick(object sender, RoutedEventArgs e) => PopupConfig.IsOpen = true;
        private void ButtonSkins_OnClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is Button button)
            {
                PopupConfig.IsOpen = false;
                if (button.Tag is ApplicationTheme tag)
                {
                    ((App)Application.Current).UpdateTheme(tag);
                    Properties.Settings.Default["Theme"] = tag.ToString();
                    Properties.Settings.Default.Save();
                }
            }
        }

        #endregion

        private void MSDL_GİTHUB_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/gravesoft/msdl/tree/main");
        }

        private void MSDLGUI_GİTHUB_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("");
        }
    }
}
