using DigitalCap.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace DigitalCap.Core.Interfaces.Repository
{
    public interface ISecurityClientRepository
        : IRepositoryBase<ClientModel, Guid>
    {

        //Task<bool> ClientNameExistsAsync(string clientName);
        //Task<bool> ClientNameExistsAsync(ClientModel clientModel);
        Task<IReadOnlyList<ClientModel>> GetAllClientsAsync();
        


    }
}
