using FluentAssertions;
using ReBuzz.Core;
using System.Collections.Generic;

namespace ReBuzzTests.Automation.TestMachinesControllers
{
    public class TestMachineInstanceCommand(DynamicMachineController controller, string commandName, object parameter)
    {
        public void Execute(
            ReBuzzCore buzzCore,
            Dictionary<string, MachineCore> machineCores)
        {
            machineCores.Should().ContainKey(controller.InstanceName);
            machineCores[controller.InstanceName].Should().NotBeNull();

            var instrument = machineCores[controller.InstanceName];
            controller.Command(instrument, commandName, buzzCore).Execute(parameter);
        }
    }
}