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

            for (int i = 1; i <= 100; i++)
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
            rent = new cds_rent();
            CarTransferReportGenerator reportsGenerator = new CarTransferReportGenerator();

            rent.cds_rent1 = $"Rent #{rentNumber}";
            rent.cds_Reservedpickup = generateRandomDate(new DateTime(2019, 1, 1), new DateTime(2020, 12, 31));
            DateTime date = (DateTime)rent.cds_Reservedpickup; // added this because cant add days to cds_Reservedpickup
            rent.cds_Reservedhandover = generateRandomDate(date, date.AddDays(30));
            rent = generateRandomCarClasssAndCar(context, rent);
            rent = generateRandomCustomer(rent, context);
            rent.cds_Pickuplocation = generateRandomPickupLocation(rent);
            rent.cds_Returnlocation = generateRandomReturnLocation(rent);
            rent = generateRandomStatus(rent);

            

            if(rent.statuscode.Value == 754300002) // if renting
            {
                rent.cds_Actualpickup = generateRandomDate(date, date.AddDays(30));
                rent = reportsGenerator.generateCarTransferReport(false, rent, service, rentNumber, context);
            }
            if (rent.statuscode.Value == 754300005) // Returned
            {
                rent.cds_Actualpickup = generateRandomDate(date, date.AddDays(30));
                date = (DateTime)rent.cds_Actualpickup;
                rent.cds_Actualreturn = generateRandomDate(date, date.AddDays(30));
                rent = reportsGenerator.generateCarTransferReport(true, rent, service, rentNumber, context);
                rent = reportsGenerator.generateCarTransferReport(false, rent, service, rentNumber, context);
            }
            rent.cds_Paid = generatePaidStatus(rent);

            service.Create(rent);
            Console.WriteLine($"Rent №{rentNumber} created with name {rent.cds_rent1}");
        }

    }

    class CarTransferReportGenerator
    {
        public cds_rent generateCarTransferReport(bool type, cds_rent rent,  CrmServiceClient service, int rentNumber, svcContext context) 
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
                rent.cds_Pickupreport = context.cds_cartransferreportSet.Single(x => x.cds_CarTransferReport == report.cds_CarTransferReport).ToEntityReference();
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
                rent.cds_Returnreport = context.cds_cartransferreportSet.Single(x => x.cds_CarTransferReport == report.cds_CarTransferReport).ToEntityReference();
            }

            return rent;
        }
    }

    class RentGenerator
    {
        private Random gen = new Random();
        protected DateTime generateRandomDate(DateTime startDate, DateTime endDate)
        {
            int range = (endDate - startDate).Days;
            return startDate.AddDays(gen.Next(range)).AddHours(gen.Next(0, 24)).AddMinutes(gen.Next(0, 60));
        }

        protected cds_rent generateRandomCarClasssAndCar(svcContext context, cds_rent rent)
        {
            var CarClasses = from a in context.cds_carclassSet
                               select a;

            List<string> carTypes = new List<string>();

            foreach (var carClass in CarClasses)
                carTypes.Add(carClass.Attributes["cds_carclass"].ToString());


            rent.cds_Carclass = context.cds_carclassSet.Single(x => x.cds_carclass1 == carTypes[gen.Next(0, carTypes.Count)]).ToEntityReference();
            rent.cds_Price = context.cds_carclassSet.Single(x => x.cds_carclassId == rent.cds_Carclass.Id).cds_Price;

            var cares = from a in context.cds_carSet
                        where a.cds_Carclass == rent.cds_Carclass
                        select a;

            List<string> cars = new List<string>();

            foreach (var car in cares)
                cars.Add(car.Attributes["cds_cars"].ToString());

            rent.cds_Car = context.cds_carSet.Single(x => x.cds_cars == cars[gen.Next(0, cars.Count)]).ToEntityReference();

            return rent;
        }

        protected cds_rent generateRandomStatus(cds_rent rent)
        {
            int chance = gen.Next(0, 100);
            if(chance <=4) // 5%  chance created
            {
                rent.statecode = cds_rentState.Active;
                rent.statuscode = new OptionSetValue(754300000);
                return rent;
            }
            else if (chance <= 9 && chance >=5) // 5% chance Confirmed
            {
                rent.statecode = cds_rentState.Active;
                rent.statuscode = new OptionSetValue(754300001);
                return rent;
            }
            else if (chance <= 14 && chance >= 10) // 5% chance Renting
            {
                rent.statecode = cds_rentState.Active;
                rent.statuscode = new OptionSetValue(754300002);
                return rent;
            }
            else if(chance <= 24 && chance >= 15) // 10% chance Canceled
            {
                rent.statecode = cds_rentState.Inactive;
                rent.statuscode = new OptionSetValue(754300006);
                return rent;
            }
            else // 75% chance Returned
            {
                rent.statecode = cds_rentState.Inactive;
                rent.statuscode = new OptionSetValue(754300005);
                return rent;
            }
        }

        protected OptionSetValue generateRandomPickupLocation(cds_rent rent) => rent.cds_Pickuplocation = new OptionSetValue(gen.Next(754300000, 754300003));

        protected OptionSetValue generateRandomReturnLocation(cds_rent rent) => rent.cds_Returnlocation = new OptionSetValue(gen.Next(754300000, 754300003));

        protected bool generatePaidStatus(cds_rent rent)
        {
            if (rent.statuscode == new OptionSetValue(754300001)) // confirmed
                return gen.Next(100) < 90;
            else if (rent.statuscode == new OptionSetValue(754300002)) // Renting
                return gen.Next(100) + gen.NextDouble() < 99.9;
            else // returned
                return gen.Next(100) + gen.NextDouble() < 99.8;
        }

        protected cds_rent generateRandomCustomer(cds_rent rent, svcContext context)
        {
            var customersEntity = from a in context.ContactSet
                                  select a;

            List<String> customers = new List<string>();

            foreach (var customer in customersEntity)
                customers.Add(customer.FullName);

            rent.cds_contact_cds_rent_Customer = context.ContactSet.Single(x => x.FullName == customers[gen.Next(0, customers.Count)]);

            return rent;
        }
    }
}
