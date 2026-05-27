using Multi_Store.Core.Entities;
using Multi_Store.Core.Reposinterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multi_Store.Services.Managers
{
    public class PaymentManager
    {
        private readonly IPaymentRepository _paymentRepository;
        private readonly IRefundRequestRepository _refundRepository;
        private readonly IOrderRepository _orderRepository;

        public PaymentManager(
            IPaymentRepository paymentRepository,
            IRefundRequestRepository refundRepository,
            IOrderRepository orderRepository)
        {
            _paymentRepository = paymentRepository;
            _refundRepository = refundRepository;
            _orderRepository = orderRepository;
        }

        // =====================================================
        // CREATE PAYMENT
        // =====================================================
        public async Task<int> CreatePaymentAsync(
            int orderId,
            string paymentMethod,
            string paymentGateway,
            decimal amount,
            string? transactionId = null)
        {
            var order = await _orderRepository.GetOrderDetailsAsync(orderId);

            if (order == null)
                throw new Exception("Order not found");

            var payment = new Payment
            {
                OrderID = orderId,
                PaymentMethod = paymentMethod,
                PaymentGateway = paymentGateway,
                GatewayTransactionID = transactionId,
                Amount = amount,
                PaymentDate = DateTime.UtcNow,
                Status = "Pending"
            };

            await _paymentRepository.AddAsync(payment);

            return payment.PaymentID;
        }

        // =====================================================
        // CONFIRM PAYMENT (SUCCESS)
        // =====================================================
        public async Task ConfirmPaymentAsync(int paymentId, string transactionId)
        {
            var payment = await _paymentRepository.GetByIdAsync(paymentId);

            if (payment == null)
                throw new Exception("Payment not found");

            payment.Status = "Paid";
            payment.GatewayTransactionID = transactionId;
            payment.PaymentDate = DateTime.UtcNow;

            await _paymentRepository.UpdateAsync(payment);

            // update order
            var order = await _orderRepository.GetByIdAsync(payment.OrderID);
            if (order != null)
            {
                order.PaymentStatus = "Paid";
                await _orderRepository.UpdateAsync(order);
            }
        }

        // =====================================================
        // FAIL PAYMENT
        // =====================================================
        public async Task FailPaymentAsync(int paymentId, string reason)
        {
            var payment = await _paymentRepository.GetByIdAsync(paymentId);

            if (payment == null)
                throw new Exception("Payment not found");

            payment.Status = "Failed";

            await _paymentRepository.UpdateAsync(payment);

            var order = await _orderRepository.GetByIdAsync(payment.OrderID);
            if (order != null)
            {
                order.PaymentStatus = "Failed";
                await _orderRepository.UpdateAsync(order);
            }
        }

        // =====================================================
        // GET LATEST PAYMENT
        // =====================================================
        public async Task<Payment> GetLatestPaymentAsync(int orderId)
        {
            var payment = await _paymentRepository.GetLatestPaymentAsync(orderId);

            if (payment == null)
                throw new Exception("No payment found");

            return payment;
        }

        // =====================================================
        // REQUEST REFUND
        // =====================================================
        public async Task<int> RequestRefundAsync(
            int orderId,
            int customerId,
            string reason,
            decimal amount,
            string? description = null,
            string? evidenceUrl = null)
        {
            var order = await _orderRepository.GetByIdAsync(orderId);

            if (order == null)
                throw new Exception("Order not found");

            if (order.PaymentStatus != "Paid")
                throw new Exception("Refund allowed only for paid orders");

            var refund = new RefundRequest
            {
                OrderID = orderId,
                CustomerID = customerId,
                Reason = reason,
                Description = description,
                EvidenceImageURL = evidenceUrl,
                RequestedAmount = amount,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            await _refundRepository.AddAsync(refund);

            return refund.RefundRequestID;
        }

        // =====================================================
        // APPROVE REFUND
        // =====================================================
        public async Task ApproveRefundAsync(int refundId, decimal approvedAmount, string adminNotes)
        {
            var refund = await _refundRepository.GetByIdAsync(refundId);

            if (refund == null)
                throw new Exception("Refund not found");

            refund.Status = "Approved";
            refund.ApprovedAmount = approvedAmount;
            refund.AdminNotes = adminNotes;
            refund.ResolvedAt = DateTime.UtcNow;

            await _refundRepository.UpdateAsync(refund);

            // update payment
            var payment = await _paymentRepository.GetLatestPaymentAsync(refund.OrderID);

            if (payment != null)
            {
                payment.Status = "Refunded";
                payment.RefundAmount = approvedAmount;
                payment.RefundDate = DateTime.UtcNow;

                await _paymentRepository.UpdateAsync(payment);
            }

            // update order
            var order = await _orderRepository.GetByIdAsync(refund.OrderID);
            if (order != null)
            {
                order.PaymentStatus = "Refunded";
                await _orderRepository.UpdateAsync(order);
            }
        }

        // =====================================================
        // REJECT REFUND
        // =====================================================
        public async Task RejectRefundAsync(int refundId, string adminNotes)
        {
            var refund = await _refundRepository.GetByIdAsync(refundId);

            if (refund == null)
                throw new Exception("Refund not found");

            refund.Status = "Rejected";
            refund.AdminNotes = adminNotes;
            refund.ResolvedAt = DateTime.UtcNow;

            await _refundRepository.UpdateAsync(refund);
        }
    }
}
