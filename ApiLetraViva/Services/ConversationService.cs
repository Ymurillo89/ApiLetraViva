using ApiLetraViva.Context;
using ApiLetraViva.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiLetraViva.Services
{
    public class ConversationService
    {
        private readonly AppDbContext _context;

        public ConversationService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Customer> GetOrCreateCustomer(
            long telegramChatId,
            string? name)
        {
            var customer = await _context.Customers
                .FirstOrDefaultAsync(x =>
                    x.TelegramChatId == telegramChatId);

            if (customer is null)
            {
                customer = new Customer
                {
                    TelegramChatId = telegramChatId,
                    Name = name
                };

                _context.Customers.Add(customer);

                await _context.SaveChangesAsync();
            }

            return customer;
        }

        public async Task<Conversation> GetOrCreateConversation(
            Guid customerId)
        {
            var conversation = await _context.Conversations
                .Include(x => x.Messages)
                .FirstOrDefaultAsync(x =>
                    x.CustomerId == customerId);

            if (conversation is null)
            {
                conversation = new Conversation
                {
                    CustomerId = customerId
                };

                _context.Conversations.Add(conversation);

                await _context.SaveChangesAsync();
            }

            return conversation;
        }

        public async Task SaveMessage(
            Guid conversationId,
            string role,
            string content)
        {
            var message = new Message
            {
                ConversationId = conversationId,
                Role = role,
                Content = content
            };

            _context.Messages.Add(message);

            await _context.SaveChangesAsync();
        }
    }
}

