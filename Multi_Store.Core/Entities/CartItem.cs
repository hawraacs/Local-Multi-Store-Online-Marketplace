using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Multi_Store.Core.Entities
{

    public class CartItem
    {
        // Primary Key
        private int cartItemID;

        // Foreign Keys
        private int cartID;
        private int productID;

        // Attributes
        private int quantity;

        // decimal is used in C# for money
        private decimal priceAtAddTime;

        // DateTime is used in C#
        private DateTime addedAt;

        // Relationships
        private Cart cart;
        private Product product;
    }
}
