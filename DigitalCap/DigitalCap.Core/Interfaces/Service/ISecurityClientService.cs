using DigitalCap.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DigitalCap.Core.Interfaces.Service
{
    public interface ISecurityClientService
    {
        //Task<bool> ClientNameExistsAsync(string clientName);

        Task<ServiceResult<IReadOnlyList<ClientModel>>> GetAllClientsAsync();

        Task<ServiceResult<ClientModel>> CreateClientAsync(ClientModel model);
    }
}

