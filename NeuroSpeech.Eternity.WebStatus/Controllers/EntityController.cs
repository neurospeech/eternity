using Microsoft.AspNetCore.Mvc;
using NeuroSpeech.EntityAccessControl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity.WebStatus.Controllers
{
    [Route("api/workflows/entity")]
    public class EntityController : BaseEntityController
    {
        public EntityController(ISecureQueryProvider db) : base(db)
        {
        }
    }
}
