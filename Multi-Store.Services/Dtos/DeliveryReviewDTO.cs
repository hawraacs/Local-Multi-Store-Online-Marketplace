using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class DeliveryReviewDTO
{
    public int DeliveryReviewID { get; set; }

    public int OrderID { get; set; }

    public int CustomerID { get; set; }

    public int DeliveryPersonID { get; set; }
    public string? DeliveryPersonName { get; set; }   // SIMPLE FLAT FIELD, same pattern as ReviewDTO.CustomerName

    public string? CustomerName { get; set; }         // SIMPLE FLAT FIELD, same pattern as ReviewDTO.CustomerName

    public int Rating { get; set; }
    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; }
}
