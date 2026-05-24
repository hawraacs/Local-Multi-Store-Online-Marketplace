using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Entities
{
    public class Wishlist
    {
        // Primary Key
        private int wishlistID;

        // Foreign Keys
        private int customerID;
        private int productID;

        // Attributes
        private DateTime addedAt;

        // Relationships

        // Many wishlist entries belong to one customer
        private Customer customer;

        // Many wishlist entries can reference one product
        private Product product;
    }
}
