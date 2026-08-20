using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace sunpath.Models
{
    public enum DispatchStatus
    {
        Pending = 0,

        // مأموریت به راننده و خودرو تخصیص داده شده
        Assigned = 1,

        // راننده مأموریت را شروع کرده است
        Started = 2,

        Completed = 3,

        Cancelled = 4
    }
}