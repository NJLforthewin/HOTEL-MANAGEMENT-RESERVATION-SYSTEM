using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Hotel_Management_System.Models
{
    public class InventoryItem
    {
        [Key]
        public int ItemId { get; set; }

        [Required]
        [Display(Name = "Item Name")]
        public string ItemName { get; set; } = string.Empty;

        [Display(Name = "Total Stock")]
        public int TotalStock { get; set; }

        [Display(Name = "In Use")]
        public int InUse { get; set; }

        [NotMapped]
        [Display(Name = "Available")]
        public int Available
        {
            get { return TotalStock - InUse; }
            private set { } // EF Core requires a setter
        }

        [Display(Name = "Reorder Level")]
        public int ReorderLevel { get; set; }

        [Display(Name = "Last Updated")]
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        [NotMapped]
        [Display(Name = "Status")]
        public string Status
        {
            get
            {
                if (Available <= 0)
                    return "Out of Stock";
                else if (Available <= ReorderLevel)
                    return "Low Stock";
                else
                    return "Well Stocked";
            }
            private set { } // EF Core requires a setter
        }
    }
}