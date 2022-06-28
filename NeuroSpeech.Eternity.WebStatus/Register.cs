using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NeuroSpeech.Eternity.WebStatus.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity.WebStatus
{
    public static class Register
    {

        public static void AddEternityWebStatus(this IServiceCollection services, 
            string connectionString, 
            string workflowTableName = "Workflows")
        {
            services.AddDbContext<EternityDbContext>((options) => { 
                options.UseSqlServer(connectionString);
            });
        }

        public static void RegisterWebStatusParts(this IMvcBuilder builder)
        {
            builder
                .AddApplicationPart(typeof(Register).Assembly)
                .AddControllersAsServices();
        }

    }
}
