using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Entities
{
    public class OrderItem
    {
        // Primary Key
        private int orderItemID;

        // Foreign Keys
        private int orderID;
        private int productID;
        private int storeID;

        // Attributes
        private string productName;

        private decimal productPrice;

        private int quantity;

        private decimal totalPrice;

        private bool reviewSubmitted;

        // Relationships

        // Many OrderItems belong to one Order
        private Order order;

        // Many OrderItems reference one Product
        private Product product;

        // Many OrderItems belong to one Store
        private Store store;
    }
}
