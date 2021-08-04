using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Tooling.Connector;
using Microsoft.Xrm.Sdk.Query;

namespace RentGenerationTool
{
    class Program
    {
        static void Main(string[] args)
        {
            UserInterface run = new UserInterface();

            string connectionString = @"AuthType=OAuth;
            Username = Tuhan@klymkopavel.onmicrosoft.com; 
            Password = Ehuvid57; 
            Url = https://org646d956a.crm4.dynamics.com; 
            AppId=51f81489-12ee-4a9e-aaae-a2591f45987d;
            RedirectUri=app://58145B91-0C36-4500-8554-080854F2AC97";

            CrmServiceClient service = new CrmServiceClient(connectionString);

            Random gen = new Random();

            for (int i = 1; i <= 40000; i++)
            {
                using (svcContext context = new svcContext(service))
                {
                    run.generateRandomRent(i, context, service);
                }
            }
            Console.ReadLine();
        }
    }

    class UserInterface : RentGenerator
    {
        private Random gen = new Random();
        private cds_rent rent;

        public void generateRandomRent(int rentNumber, svcContext context, CrmServiceClient service)
        {
            Console.WriteLine($"Creating №{rentNumber} \t {DateTime.Now}");
            rent = new cds_rent();
            CarTransferReportGenerator reportsGenerator = new CarTransferReportGenerator();

            rent.cds_rent1 = $"Rent #{rentNumber}";
            rent.cds_Reservedpickup = generateRandomDate(new DateTime(2019, 1, 1), new DateTime(2020, 12, 31));
            DateTime date = (DateTime)rent.cds_Reservedpickup;
            rent.cds_Reservedhandover = generateRandomDate(date, date.AddDays(30));
            rent.cds_contact_cds_rent_Customer = generateRandomCustomer(rent, context);
            rent.cds_Pickuplocation = generateRandomPickupLocation(rent);
            rent.cds_Returnlocation = generateRandomReturnLocation(rent);
            generateRandomStatus(ref rent);
            generateRandomCarClasssAndCar(context, ref rent);

            if (rent.statuscode.Value == (int)statusOptionSet.renting) // if renting
            {
                rent.cds_Actualpickup = generateRandomDate(date, date.AddDays(30));
                rent.cds_Pickupreport = reportsGenerator.generateCarTransferReport(false, rent, service, rentNumber, context).ToEntityReference();
            }
            if (rent.statuscode.Value == (int)statusOptionSet.returned) // Returned
            {
                rent.cds_Actualpickup = generateRandomDate(date, date.AddDays(30));
                date = (DateTime)rent.cds_Actualpickup;
                rent.cds_Actualreturn = generateRandomDate(date, date.AddDays(30));
                rent.cds_Returnreport = reportsGenerator.generateCarTransferReport(true, rent, service, rentNumber, context).ToEntityReference();
                rent.cds_Pickupreport = reportsGenerator.generateCarTransferReport(false, rent, service, rentNumber, context).ToEntityReference();
            }
            rent.cds_Paid = generatePaidStatus(rent);

            service.Create(rent);
            Console.WriteLine($"Rent №{rentNumber} created with name {rent.cds_rent1} \t {DateTime.Now}");
        }

    }

    class CarTransferReportGenerator
    {
        public cds_cartransferreport generateCarTransferReport(bool type, cds_rent rent,  CrmServiceClient service, int rentNumber, svcContext context) 
        {
            cds_cartransferreport report = new cds_cartransferreport();
            Random gen = new Random();

            if (type == false) // Pickup
            {
                report.cds_CarTransferReport = $"Pickup report to rent #{rentNumber}";
                report.cds_Date = rent.cds_Actualpickup;
                report.cds_Type = type;
                report.cds_Car = rent.cds_Car;
                service.Create(report);
                return context.cds_cartransferreportSet.Single(x => x.cds_cartransferreportId == report.cds_cartransferreportId);
            }
            else // Return
            {
                report.cds_CarTransferReport = $"Return report to rent #{rentNumber}";
                report.cds_Date = rent.cds_Actualreturn;
                report.cds_Type = type;
                int damageChance = gen.Next(100);
                if (damageChance <= 4 && damageChance >= 0) // damage chance
                {
                    report.cds_Damages = true;
                    report.cds_Damagedescription = "damage";
                }
                report.cds_Car = rent.cds_Car;
                service.Create(report);
                return context.cds_cartransferreportSet.Single(x => x.cds_cartransferreportId == report.cds_cartransferreportId);
            }
        }
    }

    class RentGenerator
    {
        private Random gen = new Random();
        private List<string> attributesList;
        protected enum statusOptionSet
        {
            created = 754300000,
            confirmed = 754300001,
            renting = 754300002,
            returned = 754300005,
            canceled = 754300006
        }
        protected enum locationOptionSet
        {
            airport = 754300000,
            cityCenter = 754300001,
            office = 754300002
        }

        protected DateTime generateRandomDate(DateTime startDate, DateTime endDate)
        {
            int range = (endDate - startDate).Days;
            return startDate.AddDays(gen.Next(range)).AddHours(gen.Next(0, 24)).AddMinutes(gen.Next(0, 60));
        }

        protected void generateRandomCarClasssAndCar(svcContext context, ref cds_rent rent)
        {
            var CarClasses = from a in context.cds_carclassSet  
                               select a;

            attributesList = new List<string>();

            foreach (var carClass in CarClasses)
                attributesList.Add(carClass.Attributes["cds_carclass"].ToString());

            rent.cds_Carclass = context.cds_carclassSet.Single(x => x.cds_carclass1 == attributesList[gen.Next(0, attributesList.Count)]).ToEntityReference();
            var cdscarclass = rent.cds_Carclass; // cant use ref parameters in querry
            rent.cds_Price = context.cds_carclassSet.Single(x => x.cds_carclassId == cdscarclass.Id).cds_Price;

            var cares = from a in context.cds_carSet
                        where a.cds_Carclass == cdscarclass
                        select a;

            attributesList= new List<string>();

            foreach (var car in cares)
                attributesList.Add(car.Attributes["cds_cars"].ToString());

            rent.cds_Car = context.cds_carSet.Single(x => x.cds_cars == attributesList[gen.Next(0, attributesList.Count)]).ToEntityReference();
        }

        protected void generateRandomStatus( ref cds_rent rent)
        {
            int chance = gen.Next(0, 100);
            if(chance <=4) // 5%  chance created
            {
                rent.statecode = cds_rentState.Active;
                rent.statuscode = new OptionSetValue((int)statusOptionSet.created);
            }
            else if (chance <= 9 && chance >=5) // 5% chance Confirmed
            {
                rent.statecode = cds_rentState.Active;
                rent.statuscode = new OptionSetValue((int)statusOptionSet.confirmed);
            }
            else if (chance <= 14 && chance >= 10) // 5% chance Renting
            {
                rent.statecode = cds_rentState.Active;
                rent.statuscode = new OptionSetValue((int)statusOptionSet.renting);
            }
            else if(chance <= 24 && chance >= 15) // 10% chance Canceled
            {
                rent.statecode = cds_rentState.Inactive;
                rent.statuscode = new OptionSetValue((int)statusOptionSet.canceled);
            }
            else // 75% chance Returned
            {
                rent.statecode = cds_rentState.Inactive;
                rent.statuscode = new OptionSetValue((int)statusOptionSet.returned);
            }
        }

        protected OptionSetValue generateRandomPickupLocation(cds_rent rent) => rent.cds_Pickuplocation = new OptionSetValue(gen.Next((int)locationOptionSet.airport, (int)locationOptionSet.office+1));

        protected OptionSetValue generateRandomReturnLocation(cds_rent rent) => rent.cds_Returnlocation = new OptionSetValue(gen.Next((int)locationOptionSet.airport, (int)locationOptionSet.office + 1));

        protected bool generatePaidStatus(cds_rent rent)
        {
            if (rent.statuscode == new OptionSetValue((int)statusOptionSet.confirmed)) // confirmed
                return gen.Next(100) < 90;  // 10% chance
            else if (rent.statuscode == new OptionSetValue((int)statusOptionSet.confirmed)) // Renting
                return gen.Next(1000) + gen.NextDouble() < 998; // 99.9% chance
            else // returned
                return gen.Next(1000) + gen.NextDouble() < 997; // 99.8% chance
        }

        protected Contact generateRandomCustomer(cds_rent rent, svcContext context)
        {
            var customersEntity = from a in context.ContactSet
                                  select a;

            attributesList = new List<string>();

            foreach (var customer in customersEntity)
                attributesList.Add(customer.FullName);

            return context.ContactSet.Single(x => x.FullName == attributesList[gen.Next(0, attributesList.Count)]); ;
        }
    }
}
