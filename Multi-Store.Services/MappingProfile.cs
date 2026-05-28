using AutoMapper;
using Multi_Store.Core.Entities;
using Multi_Store.Services.Dtos;
namespace Multi_Store.Services
{
  
    using static System.Runtime.InteropServices.JavaScript.JSType;

    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<AuditLog, AuditLogDTO>().ReverseMap();
            CreateMap<Cart, CartDTO>().ReverseMap();
            CreateMap<CartItem, CartItemDTO>().ReverseMap();
            CreateMap<Category, CategoryDTO>().ReverseMap();
            CreateMap<ChatMessage, ChatMessageDTO>().ReverseMap();
            CreateMap<Complaint, ComplaintDTO>().ReverseMap();
            CreateMap<Coupon, CouponDTO>().ReverseMap();
            CreateMap<CustomerAddress, CustomerAddressDTO>().ReverseMap();
            CreateMap<Customer, CustomerDTO>().ReverseMap();
            CreateMap<DeliveryArea, DeliveryAreaDTO>().ReverseMap();
            CreateMap<DeliveryAssignment, DeliveryAssignmentDTO>().ReverseMap();
            CreateMap<DeliveryPerson, DeliveryPersonDTO>().ReverseMap();
            CreateMap<Notification, NotificationDTO>().ReverseMap();
            CreateMap<Order, OrderDTO>().ReverseMap();
            CreateMap<OrderItem, OrderItemDTO>().ReverseMap();
            CreateMap<OrderStatusHistory, OrderStatusHistoryDTO>().ReverseMap();
            CreateMap<Payment, PaymentDTO>().ReverseMap();
            CreateMap<Product, ProductDTO>().ReverseMap();
            CreateMap<ProductImage, ProductImageDTO>().ReverseMap();
            CreateMap<RefundRequest, RefundRequestDTO>().ReverseMap();
            CreateMap<Review, ReviewDTO>().ReverseMap();
            
            CreateMap<Session, SessionDTO>().ReverseMap();
            CreateMap<Store, StoreDTO>().ReverseMap();
            CreateMap<SystemConfig, SystemConfigDTO>().ReverseMap();
            CreateMap<User, UserDTO>().ReverseMap();
            CreateMap<Wishlist, WishlistDTO>().ReverseMap();
        
        }
    }


}
