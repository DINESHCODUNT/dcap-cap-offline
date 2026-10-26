//using Microsoft.AspNetCore.Identity.UI.Services;
using DigitalCap.Core.Interfaces.Service;
using System;
using System.Collections.Generic;
using System.Text;

namespace DigitalCap.Infrastructure.Service
{
    public class EmailSender : IEmailSender
    {
        public Task SendEmailAsync(string toEmail, string subject, string message, string fromEmail)
        {
            //var client = new SendGridClient(apiKey);
            //var from = new EmailAddress(fromEmail);
            //var to = new EmailAddress(toEmail);

            //var msg = MailHelper.CreateSingleEmail(from, to, subject, message, message);

            //await client.SendEmailAsync(msg);
        
            return Task.CompletedTask;
        }
    }
}