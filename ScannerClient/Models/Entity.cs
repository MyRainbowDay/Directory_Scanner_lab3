using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScannerClient.Models
{
    // This classes are not used because we take the same implementation from our dll, but having them there can be good
    // if we want to check models structure without going to the dll file

    // Main entity class (files and directories)
    public class Entity
    {
        public FileSystemInfo Info { get; set; } // Системная информация о сущности
        public string Name { get; set; } // Имя
        public EntityType Type { get; set; } // Тип сущности
        public DirectoryInfo SubDirecory { get; set; } // Каталог, в котором содержится сущность (null для головной)
        public long? Size { get; set; } = null; // размер (в байтах)
        public string Persantage { get; set; } = null; // размер (в процентах от всего содержимого каталога)

        public Entity() { }

    }

    // Enum for saving the type of entities
    public enum EntityType
    {
        Directory = 1,
        File = 2,
        TextFile = 3
    }
}
