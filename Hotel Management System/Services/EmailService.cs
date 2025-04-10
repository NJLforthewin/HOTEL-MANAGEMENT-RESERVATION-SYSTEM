using Microsoft.Extensions.Configuration;
using MimeKit;
using MailKit.Net.Smtp;
using System;
using System.Threading.Tasks;

namespace Hotel_Management_System.Services
{
    public class EmailService
    {
        private readonly string _smtpServer;
        private readonly int _smtpPort;
        private readonly string _smtpUsername;
        private readonly string _smtpPassword;
        private readonly string _senderEmail;
        private readonly string _senderName;

        public EmailService(IConfiguration configuration)
        {
            // Add null checks and default values
            _smtpServer = configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
            _smtpPort = int.TryParse(configuration["EmailSettings:SmtpPort"], out int port) ? port : 587;
            _smtpUsername = configuration["EmailSettings:SmtpUsername"] ?? string.Empty;
            _smtpPassword = configuration["EmailSettings:SmtpPassword"] ?? string.Empty;
            _senderEmail = configuration["EmailSettings:SenderEmail"] ?? string.Empty;
            _senderName = configuration["EmailSettings:SenderName"] ?? "Nuxus Hotel";

            // Validate required settings
            if (string.IsNullOrEmpty(_smtpUsername) || string.IsNullOrEmpty(_smtpPassword))
            {
                throw new ArgumentException("Email settings are not properly configured. Please check your appsettings.json file.");
            }
        }

        public async Task<bool> SendBookingConfirmationAsync(
            string toEmail,
            string guestName,
            string bookingReference,
            DateTime checkIn,
            DateTime checkOut,
            string roomNumber,
            decimal totalAmount,
            string paymentMethod = "Credit Card")
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_senderName, _senderEmail));
                message.To.Add(new MailboxAddress(guestName ?? "Valued Guest", toEmail));
                message.Subject = $"Your Booking Receipt #{bookingReference} - Nuxus Hotel - {paymentMethod}";

                // Create HTML email body with receipt format
                var bodyBuilder = new BodyBuilder();
                bodyBuilder.HtmlBody = $@"
        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd;'>
            <div style='background-color: #800020; color: white; padding: 15px; text-align: center;'>
                <h1 style='margin: 0;'>Booking Receipt</h1>
            </div>
            
            <div style='padding: 20px;'>
                <p>Dear {(string.IsNullOrEmpty(guestName) ? "Valued Guest" : guestName)},</p>
                <p>Thank you for choosing Nuxus Hotel. Please find your booking receipt below:</p>
                
                <div style='border: 1px solid #ddd; border-radius: 5px; margin: 20px 0;'>
                    <!-- Receipt Header -->
                    <div style='background-color: #f5f5f5; padding: 10px; border-bottom: 1px solid #ddd;'>
                        <table style='width: 100%;'>
                            <tr>
                                <td style='width: 50%;'>
                                    <h2 style='margin: 0; color: #800020;'>Nuxus Hotel</h2>
                                    <p style='margin: 5px 0 0 0;'>Receipt #: {bookingReference}</p>
                                </td>
                                <td style='width: 50%; text-align: right;'>
                                    <p style='margin: 0;'>Date: {DateTime.Now:MMMM d, yyyy}</p>
                                </td>
                            </tr>
                        </table>
                    </div>
                    
                    <!-- Guest Information -->
                    <div style='padding: 15px; border-bottom: 1px solid #ddd;'>
                        <h3 style='margin-top: 0; color: #800020;'>Guest Information</h3>
                        <p><strong>Name:</strong> {(string.IsNullOrEmpty(guestName) ? "Valued Guest" : guestName)}</p>
                        <p><strong>Booking Reference:</strong> #{bookingReference}</p>
                    </div>
                    
                    <!-- Booking Details -->
                    <div style='padding: 15px; border-bottom: 1px solid #ddd;'>
                        <h3 style='margin-top: 0; color: #800020;'>Booking Details</h3>
                        <p><strong>Room Number:</strong> {roomNumber}</p>
                        <p><strong>Check-in Date:</strong> {checkIn:dddd, MMMM d, yyyy}</p>
                        <p><strong>Check-out Date:</strong> {checkOut:dddd, MMMM d, yyyy}</p>
                        <p><strong>Number of Nights:</strong> {(checkOut - checkIn).Days}</p>
                    </div>
                    
                    <!-- Payment Information -->
                    <div style='padding: 15px;'>
                        <h3 style='margin-top: 0; color: #800020;'>Payment Information</h3>
                        <p><strong>Payment Method:</strong> {paymentMethod}</p>
                        <table style='width: 100%; border-collapse: collapse;'>
                            <tr style='border-bottom: 1px solid #ddd;'>
                                <th style='text-align: left; padding: 8px;'>Description</th>
                                <th style='text-align: right; padding: 8px;'>Amount</th>
                            </tr>
                            <tr>
                                <td style='padding: 8px;'>Room Charge ({(checkOut - checkIn).Days} nights)</td>
                                <td style='text-align: right; padding: 8px;'>₱{totalAmount:N2}</td>
                            </tr>
                            <tr style='border-top: 1px solid #ddd; font-weight: bold;'>
                                <td style='padding: 8px;'>Total</td>
                                <td style='text-align: right; padding: 8px;'>₱{totalAmount:N2}</td>
                            </tr>
                            <tr>
                                <td colspan='2' style='padding: 8px; text-align: right;'>
                                    <p style='color: green; margin: 0;'><strong>PAID</strong></p>
                                </td>
                            </tr>
                        </table>
                    </div>
                </div>
                
                <p>We look forward to welcoming you to Nuxus Hotel!</p>
                
                <p>If you have any questions or need assistance, please don't hesitate to contact us.</p>
                
                <p>Warm regards,<br>
                Nuxus Hotel Team</p>
            </div>
            
            <div style='background-color: #f2f2f2; padding: 15px; text-align: center; font-size: 12px; color: #666;'>
                <p>This is an official receipt for your booking. Please retain for your records.</p>
                <p>© 2025 Nuxus Hotel. All rights reserved.</p>
                <p>123 Main Street, Anytown, Philippines | Tel: +123 456 7890 | Email: info@nuxushotel.com</p>
            </div>
        </div>";

                message.Body = bodyBuilder.ToMessageBody();

                using (var client = new SmtpClient())
                {
                    client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                    await client.ConnectAsync(_smtpServer, _smtpPort, MailKit.Security.SecureSocketOptions.StartTls);
                    await client.AuthenticateAsync(_smtpUsername, _smtpPassword);
                    await client.SendAsync(message);
                    await client.DisconnectAsync(true);
                }

                Console.WriteLine($"Email receipt sent successfully to {toEmail}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Email sending failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SendBankTransferInstructionsAsync(
            string toEmail,
            string guestName,
            string bookingReference,
            DateTime checkIn,
            DateTime checkOut,
            string roomNumber,
            decimal amount)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_senderName, _senderEmail));
                message.To.Add(new MailboxAddress(guestName ?? "Valued Guest", toEmail));
                message.Subject = "Bank Transfer Instructions for Booking #" + bookingReference;

                var builder = new BodyBuilder();

                // HTML body
                builder.HtmlBody = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd;'>
                    <div style='background-color: #800020; color: white; padding: 15px; text-align: center;'>
                        <h1 style='margin: 0;'>Nuxus Hotel</h1>
                        <h2 style='margin: 10px 0 0 0;'>Bank Transfer Instructions</h2>
                    </div>
                    
                    <div style='padding: 20px;'>
                        <p>Dear {(string.IsNullOrEmpty(guestName) ? "Valued Guest" : guestName)},</p>
                        
                        <p>Thank you for your booking. To complete your reservation, please transfer the payment amount to our bank account:</p>
                        
                        <div style='background-color: #f9f9f9; padding: 15px; margin: 20px 0; border-left: 4px solid #800020;'>
                            <p><strong>Bank Name:</strong> Metrobank</p>
                            <p><strong>Account Name:</strong> Nuxus Hotel</p>
                            <p><strong>Account Number:</strong> 1234-5678-9012-3456</p>
                            <p><strong>Amount:</strong> ₱{amount:N2}</p>
                            <p><strong>Reference:</strong> Booking #{bookingReference}</p>
                        </div>
                        
                        <h3 style='color: #800020;'>Booking Details:</h3>
                        <div style='background-color: #f5f5f5; padding: 15px; margin-bottom: 20px;'>
                            <p><strong>Booking Reference:</strong> #{bookingReference}</p>
                            <p><strong>Check-in Date:</strong> {checkIn:MMM dd, yyyy}</p>
                            <p><strong>Check-out Date:</strong> {checkOut:MMM dd, yyyy}</p>
                            <p><strong>Room Number:</strong> {roomNumber}</p>
                            <p><strong>Total Amount:</strong> ₱{amount:N2}</p>
                        </div>
                        
                        <div style='margin-top: 30px; padding: 15px; border: 1px solid #ffd700; background-color: #fffdf5;'>
                            <p style='margin-top: 0;'><strong>Important Notes:</strong></p>
                            <ul>
                                <li>Please complete your payment within 24 hours to confirm your booking.</li>
                                <li>Include the booking reference number in your payment description.</li>
                                <li>After making the payment, please send the receipt to info@nuxushotel.com.</li>
                            </ul>
                        </div>
                        
                        <p>If you have any questions, please contact our support team at info@nuxushotel.com.</p>
                        
                        <p>Thank you for choosing Nuxus Hotel!</p>
                        
                        <p>Warm regards,<br>
                        Nuxus Hotel Team</p>
                    </div>
                    
                    <div style='background-color: #f2f2f2; padding: 15px; text-align: center; font-size: 12px; color: #666;'>
                        <p>© 2025 Nuxus Hotel. All rights reserved.</p>
                        <p>123 Main Street, Anytown, Philippines | Tel: +123 456 7890 | Email: info@nuxushotel.com</p>
                    </div>
                </div>";

                message.Body = builder.ToMessageBody();

                using (var client = new SmtpClient())
                {
                    client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                    await client.ConnectAsync(_smtpServer, _smtpPort, MailKit.Security.SecureSocketOptions.StartTls);
                    await client.AuthenticateAsync(_smtpUsername, _smtpPassword);
                    await client.SendAsync(message);
                    await client.DisconnectAsync(true);
                }

                Console.WriteLine($"Bank transfer instructions email sent successfully to {toEmail}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending bank transfer email: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                return false;
            }
        }
    }
}