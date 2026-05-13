using ApiLetraViva.Context;
using ApiLetraViva.Enums;
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

        public async Task<List<Message>> GetRecentMessages(
            Guid conversationId,
            int count = 10)
        {
            return await _context.Messages
                .Where(m => m.ConversationId == conversationId)
                .OrderByDescending(m => m.CreatedAt)
                .Take(count)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();
        }

        public async Task UpdateConversationState(
            Guid conversationId,
            ConversationState state)
        {
            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation is not null)
            {
                conversation.State = state;
                conversation.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateCustomerEmail(
            Guid customerId,
            string email)
        {
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Id == customerId);

            if (customer is not null)
            {
                customer.Email = email;
                await _context.SaveChangesAsync();
            }
        }
    }
}

