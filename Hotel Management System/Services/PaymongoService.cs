using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Configuration;

namespace Hotel_Management_System.Services
{
    public class PaymongoService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string? _secretKey;
        private readonly string? _publicKey;

        public PaymongoService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _secretKey = configuration["Paymongo:SecretKey"];
            _publicKey = configuration["Paymongo:PublicKey"];

            if (string.IsNullOrEmpty(_secretKey))
            {
                throw new ArgumentException("Paymongo:SecretKey is not configured in appsettings.json");
            }
        }

        public async Task<JObject?> CreatePaymentIntent(decimal amount, string currency, string description)
        {
            try
            {
                using (var httpClient = _httpClientFactory.CreateClient())
                {
                    // Set up the authentication header
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                        "Basic",
                        Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{_secretKey}:")));

                    // Prepare the request body
                    var requestBody = new
                    {
                        data = new
                        {
                            attributes = new
                            {
                                amount = (int)(amount * 100), // Convert to centavos
                                payment_method_allowed = new[] { "card" },
                                payment_method_options = new
                                {
                                    card = new
                                    {
                                        request_three_d_secure = "any"
                                    }
                                },
                                currency = currency,
                                description = description
                            }
                        }
                    };

                    // Convert the request body to JSON using Newtonsoft.Json
                    var jsonContent = JsonConvert.SerializeObject(requestBody);
                    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                    // Make the API request
                    var response = await httpClient.PostAsync("https://api.paymongo.com/v1/payment_intents", content);
                    var responseBody = await response.Content.ReadAsStringAsync();

                    // Parse the response using Newtonsoft.Json
                    if (response.IsSuccessStatusCode)
                    {
                        return JObject.Parse(responseBody);
                    }
                    else
                    {
                        Console.WriteLine($"Error creating payment intent: {responseBody}");
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in CreatePaymentIntent: {ex.Message}");
                return null;
            }
        }

        public async Task<JObject?> CreateEWalletPayment(decimal amount, string type, string description, string returnUrl)
        {
            try
            {
                // Ensure returnUrl is not null or empty
                if (string.IsNullOrEmpty(returnUrl))
                {
                    returnUrl = "/Booking/EWalletCallback"; // Fallback URL
                }

                using (var httpClient = _httpClientFactory.CreateClient())
                {
                    // Set up the authentication header
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                        "Basic",
                        Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{_secretKey}:")));

                    // Prepare the request body
                    var requestBody = new
                    {
                        data = new
                        {
                            attributes = new
                            {
                                amount = (int)(amount * 100), // Convert to centavos
                                description = description,
                                currency = "PHP",
                                type = type.ToLower(), // "gcash" or "grab_pay" or "paymaya"
                                billing = new
                                {
                                    name = "Nuxus Hotel Guest",
                                    email = "guest@example.com", // This should be dynamically set
                                    phone = "09123456789"        // This should be dynamically set
                                },
                                redirect = new
                                {
                                    success = returnUrl + "?status=success",
                                    failed = returnUrl + "?status=failed"
                                }
                            }
                        }
                    };

                    // Convert the request body to JSON using Newtonsoft.Json
                    var jsonContent = JsonConvert.SerializeObject(requestBody);
                    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                    // Make the API request
                    var response = await httpClient.PostAsync("https://api.paymongo.com/v1/sources", content);
                    var responseBody = await response.Content.ReadAsStringAsync();

                    // Parse the response using Newtonsoft.Json
                    if (response.IsSuccessStatusCode)
                    {
                        return JObject.Parse(responseBody);
                    }
                    else
                    {
                        Console.WriteLine($"Error creating e-wallet payment: {responseBody}");
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in CreateEWalletPayment: {ex.Message}");
                return null;
            }
        }

        public async Task<JObject?> RetrievePaymentIntent(string paymentIntentId)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    // Add authorization header
                    string authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_secretKey}:"));
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

                    // Create request
                    var apiUrl = $"https://api.paymongo.com/v1/payment_intents/{paymentIntentId}";

                    // Send request
                    var response = await httpClient.GetAsync(apiUrl);
                    var responseContent = await response.Content.ReadAsStringAsync();

                    // Parse and return the response
                    if (response.IsSuccessStatusCode)
                    {
                        return JObject.Parse(responseContent);
                    }
                    else
                    {
                        Console.WriteLine($"Error retrieving payment intent: {responseContent}");
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in RetrievePaymentIntent: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> CheckSourceStatus(string sourceId)
        {
            try
            {
                if (string.IsNullOrEmpty(sourceId))
                {
                    return false;
                }

                using (var httpClient = _httpClientFactory.CreateClient())
                {
                    // Set up the authentication header
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                        "Basic",
                        Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{_secretKey}:")));

                    // Make the API request
                    var response = await httpClient.GetAsync($"https://api.paymongo.com/v1/sources/{sourceId}");
                    var responseBody = await response.Content.ReadAsStringAsync();

                    // Parse the response using Newtonsoft.Json
                    if (response.IsSuccessStatusCode)
                    {
                        var sourceData = JObject.Parse(responseBody);
                        var status = sourceData["data"]?["attributes"]?["status"]?.ToString();
                        return status == "chargeable";
                    }

                    Console.WriteLine($"Error checking source status: {responseBody}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in CheckSourceStatus: {ex.Message}");
                return false;
            }
        }

        public async Task<JObject?> CreatePaymentFromSource(string sourceId, decimal amount, string description)
        {
            try
            {
                if (string.IsNullOrEmpty(sourceId))
                {
                    return null;
                }

                using (var httpClient = _httpClientFactory.CreateClient())
                {
                    // Set up the authentication header
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                        "Basic",
                        Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{_secretKey}:")));

                    // Prepare the request body
                    var requestBody = new
                    {
                        data = new
                        {
                            attributes = new
                            {
                                amount = (int)(amount * 100), // Convert to centavos
                                description = description,
                                currency = "PHP",
                                source = new
                                {
                                    id = sourceId,
                                    type = "source"
                                }
                            }
                        }
                    };

                    // Convert the request body to JSON using Newtonsoft.Json
                    var jsonContent = JsonConvert.SerializeObject(requestBody);
                    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                    // Make the API request
                    var response = await httpClient.PostAsync("https://api.paymongo.com/v1/payments", content);
                    var responseBody = await response.Content.ReadAsStringAsync();

                    // Parse the response using Newtonsoft.Json
                    if (response.IsSuccessStatusCode)
                    {
                        return JObject.Parse(responseBody);
                    }
                    else
                    {
                        Console.WriteLine($"Error creating payment from source: {responseBody}");
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in CreatePaymentFromSource: {ex.Message}");
                return null;
            }
        }
    }
}