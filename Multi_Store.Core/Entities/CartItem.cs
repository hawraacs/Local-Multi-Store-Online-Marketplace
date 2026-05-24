using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Multi_Store.Core.Entities
{

    import java.Math.BigDecimal;
    import java.time.LocalDateTime;

    public class CartItem
    {

        // Primary Key
        private int cartItemID;

        // Foreign Keys
        private int cartID;
        private int productID;

        // Attributes
        private int quantity;
        private BigDecimal priceAtAddTime;
        private LocalDateTime addedAt;

        // Relationships

        // Many CartItems belong to one Cart
        private Cart cart;

        // Many CartItems reference one Product
        private Product product;
    }
}
