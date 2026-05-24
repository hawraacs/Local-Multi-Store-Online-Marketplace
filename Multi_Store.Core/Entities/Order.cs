using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Core.Entities
{
    public class Order
    {
        // Primary Key
        private int orderID;

        // Foreign Keys
        private int customerID;
        private int addressID;

        // Attributes
        private string orderNumber;
        private DateTime orderDate;

        private string status;
        private string paymentMethod;
        private string paymentStatus;

        private decimal subtotal;
        private decimal deliveryFee;
        private decimal discountAmount;
        private decimal taxAmount;
        private decimal totalAmount;

        private string cancellationReason;
        private DateTime? cancelledAt;

        private string notes;

        // Relationships

        // Many Orders belong to one Customer
        private Customer customer;

        // Many Orders can use one Address
        private CustomerAddress address;
    }

}
