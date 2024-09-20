using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitcoinAddressValidator
{
    public class BitcoinAddressRecord
    {
        public int Id { get; set; }
        public string Address { get; set; }
        public string Type { get; set; }
        public decimal Balance { get; set; }
    }

}
