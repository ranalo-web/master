using System.ComponentModel.DataAnnotations;

namespace Ranalo.DataStore
{
    public class Product
    {
        [Key]
        public int ID { get; set; }
    }
}