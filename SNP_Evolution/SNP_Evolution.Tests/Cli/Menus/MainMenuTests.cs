using System.Reflection;
using SnpEvolution.Application;
using SnpEvolution.Cli;

namespace SnpEvolution.Tests.Cli.Menus
{
    // The menu and the command line are one program: every command is on the menu, and every setting the menu shows is
    // an option of a command, so neither can do what the other cannot.
    public class MainMenuTests
    {
        [Fact]
        public void EveryCommandIsOnTheMenu()
        {
            HashSet<Command> onMenu = MainMenu.Root.Commands().Select(item => item.Command).ToHashSet();

            Assert.Empty(CommandRegistry.All.Where(command => !onMenu.Contains(command)).Select(command => command.Name));
        }

        [Fact]
        public void EveryDeclaredSettingIsListed()
        {
            IEnumerable<object?> declared = typeof(SettingOptions).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => typeof(SettingOption).IsAssignableFrom(field.FieldType))
                .Select(field => field.GetValue(null));

            Assert.All(declared, setting => Assert.Contains((SettingOption)setting!, SettingOptions.All));
        }

        [Fact]
        public void EverySettingIsOnExactlyOneSettingsPage() =>
            Assert.All(SettingOptions.All, setting => Assert.Single(SettingsMenu.Groups, group => group.Options.Contains(setting)));

        [Fact]
        public void EverySettingIsAnOptionOfSomeCommand() =>
            Assert.All(SettingOptions.All, setting => Assert.Contains(CommandRegistry.All, command => command.Options.Contains(setting.Option)));

        // The menu writes a setting into a command line as its text, so the text must read back as the same setting.
        [Fact]
        public void EverySettingsTextReadsBackAsItself()
        {
            foreach (Settings settings in new[] { new Settings(), Settings.Defaults(), ComposeService.Starting(new Settings()) })
            {
                foreach (SettingOption setting in SettingOptions.All.Where(setting => setting.Text(settings).Length > 0))
                {
                    Settings copy = new Settings();

                    Assert.True(setting.TryApply(copy, setting.Text(settings)), setting.Option.Flag);
                    Assert.Equal(setting.Text(settings), setting.Text(copy));
                }
            }
        }
    }
}
