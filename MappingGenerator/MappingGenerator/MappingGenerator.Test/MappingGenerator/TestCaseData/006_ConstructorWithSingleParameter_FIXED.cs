﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.ObjectModel;

namespace MappingGenerator.Test.MappingGenerator.TestCaseData
{
    public class UserDTO
    {
        public string FirstName { get; }
        public string LastName { get; private set; }
        public int Age { get; set; }
        public int Cash { get;}
        public AccountDTO Account { get; private set; }
        public List<AccountDTO> Debs { get; set; }
        public UserSourceDTO Source { get; set; }
        public string Login { get; set; }
        public byte[] ImageData { get; set; }
        public List<int> LuckyNumbers { get; set; }
        public int Total { get; set; }
        public AddressDTO MainAddress { get; set; }
        public ReadOnlyCollection<AddressDTO> Addresses { get; set; }
        public int UnitId { get; set; }

        private UserDTO(UserEntity entity)
        {
            FirstName = entity.FirstName;
            LastName = entity.LastName;
            Age = entity.Age;
            Cash = (int)entity.Cash;
            Account = new AccountDTO()
            {
                BankName = entity.Account.BankName,
                Number = entity.Account.Number
            };
            Debs = entity.Debs.Select(entityDeb => new AccountDTO()
            {
                BankName = entityDeb.BankName,
                Number = entityDeb.Number
            }).ToList();
            Source = new UserSourceDTO(providerName: entity.Source.ProviderName, providerAddress: entity.Source.ProviderAddress);
            ImageData = entity.ImageData;
            LuckyNumbers = entity.LuckyNumbers;
            Total = entity.GetTotal();
            MainAddress = new AddressDTO()
            {
                City = entity.MainAddress.City,
                ZipCode = entity.MainAddress.ZipCode,
                Street = entity.MainAddress.Street,
                FlatNo = entity.MainAddress.FlatNo,
                BuildingNo = entity.MainAddress.BuildingNo
            };
            Addresses = entity.Addresses.Select(entityAddresse => new AddressDTO()
            {
                City = entityAddresse.City,
                ZipCode = entityAddresse.ZipCode,
                Street = entityAddresse.Street,
                FlatNo = entityAddresse.FlatNo,
                BuildingNo = entityAddresse.BuildingNo
            }).ToList().AsReadOnly();
            UnitId = entity.Unit.Id;
        }
    }

    public class UserSourceDTO
    {
        public string ProviderName { get; set; }
        public string ProviderAddress { get; set; }

        public UserSourceDTO(string providerName, string providerAddress)
        {
            ProviderName = providerName;
            ProviderAddress = providerAddress;
        }
    }
    
    public class AccountDTO
    {
        public string BankName { get; set; }
        public string Number { get; set; }
    }

    public class AddressDTO
    {
        public string City { get; set; }
        public string ZipCode { get; set; }
        public string Street { get; set; }
        public string FlatNo { get; set; }
        public string BuildingNo { get; set; }
    }

    //---- Entities

    public class UserEntity
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public int Age { get; set; }
        public AccountEntity Account { get; set; }
        public List<AccountEntity> Debs { get; set; }
        public UserSourceEntity Source { get; set; }
        public string Name { get;  }
        public CustomerKind Kind { get; set; }
        public double Cash { get; }
        public byte[] ImageData { get; set; }
        public List<int> LuckyNumbers { get; set; }
        public AddressEntity MainAddress { get; set; }
        public string AddressCity { get; set; }
        public List<AddressEntity> Addresses { get; set; }
        public UnitEntity Unit { get; set; }

        public int GetTotal()
        {
            throw new NotImplementedException();
        }
    }

    public class AddressEntity
    {   
        public string City { get; set; }
        public string ZipCode { get; set; }
        public string Street { get; set; }
        public string FlatNo { get; set; }
        public string BuildingNo { get; set; }
    }

    public class UnitEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class UserSourceEntity
    {
        public string ProviderName { get; set; }
        public string ProviderAddress { get; set; }
    }

    public class AccountEntity
    {
        public string BankName { get; set; }
        public string Number { get; set; }
    }
}