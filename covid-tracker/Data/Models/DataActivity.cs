﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace covid_tracker.Data.Models
{
    public class DataActivity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public DateTime Timestamp { get; set; }

        public List<DataActivityPoint> ActivityPoints { get; set; }

    }
}
