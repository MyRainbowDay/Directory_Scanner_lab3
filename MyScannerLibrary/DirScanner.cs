using System.Diagnostics;
using System.IO;

namespace DirectoryScanner
{
    // Enum for saving the type of entities
    public enum EntityType
    {
        Directory = 1,
        File = 2,
        TextFile = 3
    }

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

    // Main scanner class for usage
    public class DirScanner
    {
        public static bool isWorking;

        public static List<Entity> Scan(string filePath)
        {
            if (filePath == null || !Directory.Exists(filePath))
                throw new Exception("Error. Directory does not exist.");

            isWorking = true;

            // List of all our entities (files and directories)
            List<Entity> entities = new List<Entity>();

            // Get head directory for proceeding
            string directoryPath = filePath;
            DirectoryInfo headDirectory = new DirectoryInfo(directoryPath);

            // Add head directory as an entity
            entities.Add(CreateEntityFromDirectory(headDirectory, isHeadDirectory: true));

            // Get files in the head folder
            var files_HeadDirectory = headDirectory.GetFiles();

            // Get all files (normal and directories). Hidden files included
            var filesAndDirectories_HeadDirectory = headDirectory.GetFileSystemInfos();

            // Create a list with only file names
            var fileNames_HeadDirectory = new List<string>();
            foreach (var file in files_HeadDirectory)
                fileNames_HeadDirectory.Add(file.Name);

            // Check every file and directory in head directory
            GetDirectoryIerarchy(entities, fileNames_HeadDirectory, filesAndDirectories_HeadDirectory);

            // Get all system threads available for usage
            ThreadPool.GetAvailableThreads(out int systemThreadsCount, out _);

            // Use this code to proceed all files asynchronously
            CalculateSizeOfAllEntities(entities, isAsync: true, numberOfThreadsToProceed: 7, numberOfSystemThreads: systemThreadsCount);

            // Use this code to proceed all files synchronously
            //CalculateSizeOfAllEntities(entities);

            var result = from entitiesWithSize in entities where entitiesWithSize.Size != null select entitiesWithSize;
            return result.ToList();
        }

        // Main method for calculating all entities size
        static void CalculateSizeOfAllEntities(List<Entity> entities, bool isAsync = false, int numberOfThreadsToProceed = 0, int numberOfSystemThreads = 0)
        {
            if (!isAsync) // If we want to proceed synchronously
            {
                foreach (var entity in entities) // Check every entity
                {
                    // If we have received the signal from WPF app - we stop activating new threaads and leave from the scan cycle 
                    // It works as: We have a static variable (it is a readonly variable for all the threads). Each time they check
                    // whether it is true or nor. As a client, we can change this variable having a link for it to false variable
                    // It means that we change data, and as we are working with a single variable, all the threads will realize that
                    // we have changed the value. It will be something like a token for our threads wich they will use.
                    if (!isWorking)
                        break;

                    if (entity.Type == EntityType.Directory) // If we work with directory
                    {
                        DirectoryInfo dir = (DirectoryInfo)entity.Info; // Use explisit cast from FileSystemInfo into DirectoryInfo
                        entity.Size = GetDirectorySize(dir); // Start methods for calculating directory size and persantage
                        entity.Persantage = entity.SubDirecory == null ? String.Empty : (100 * (float)GetDirectorySize(dir) / GetDirectorySize(dir.Parent)).ToString() + "%";
                    }
                    else // If we work with file
                    {
                        FileInfo file = (FileInfo)entity.Info; // Use explisit cast from FileSystemInfo into FileInfo
                        entity.Size = file.Length; // Calculate file size and persanatge
                        entity.Persantage = (100 * (float)file.Length / GetDirectorySize(file.Directory)).ToString() + "%";
                    }
                }
            }
            else // If we want to proceed asynchronously
            {
                foreach (var entity in entities)  // Check every entity
                {
                    // Same logic as above
                    if (!isWorking)
                        break;

                    ThreadPool.QueueUserWorkItem(TaskForAnAsyncCalculation, entity); // Add method for calculating size into the ThreadPool

                    while (true) // Method for checking whether we have an open thread
                    {
                        ThreadPool.GetAvailableThreads(out int currentAvailableThreads, out _); // Get all available threads at the moment

                        // If the difference between max threads count and current available threads < number of user number of threads ->
                        // -> continue foreach cycle iteration
                        if (numberOfSystemThreads - currentAvailableThreads < numberOfThreadsToProceed)
                            break;
                    }
                }
                while (true) // Cycle need to check if we have some working threads at the moment
                {
                    ThreadPool.GetAvailableThreads(out int currentAvailableThreadsCount, out _);
                    if (currentAvailableThreadsCount != numberOfSystemThreads) // If we have some of them working
                        Thread.Sleep(100); // Wait 100 ms and check again (immitation of waiting our unfinished threads) 
                    else
                        break; // If there is no -> do not wait
                }
            }

            // This cycle need to check if there are some working processes after we canceled gauging directory from client WPF side
            while (true)
            {
                ThreadPool.GetAvailableThreads(out int currentAvailableThreadsCount, out _);
                if (currentAvailableThreadsCount != numberOfSystemThreads) // If we have some of them working
                    Thread.Sleep(100); // Wait 100 ms and check again (immitation of waiting our unfinished threads) 
                else
                    break; // If there is no -> do not wait
            }
        }

        // Method to use in the ThreadPool for proceeding calculation asynchroniously
        static void TaskForAnAsyncCalculation(object entityObj)
        {
            Entity entity = (Entity)entityObj; // Explicit cast from object type to the entity
            if (entity.Type == EntityType.Directory) // If we work with directory entity
            {
                DirectoryInfo dir = (DirectoryInfo)entity.Info; // Use explisit cast from FileSystemInfo into DirectoryInfo
                entity.Size = GetDirectorySize(dir); // Start methods for calculating directory size and persantage
                entity.Persantage = entity.SubDirecory == null ? String.Empty : (100 * (float)GetDirectorySize(dir) / GetDirectorySize(dir.Parent)).ToString() + "%";
            }
            else
            {
                FileInfo file = (FileInfo)entity.Info; // Use explisit cast from FileSystemInfo into FileInfo
                entity.Size = file.Length; // Calculate file size and persanatge
                entity.Persantage = (100 * (float)file.Length / GetDirectorySize(file.Directory)).ToString() + "%";
            }
        }

        // Method for working with directories and files in the head directory and proceed through them
        static void GetDirectoryIerarchy(List<Entity> entities, List<string> fileNames_HeadDirectory, FileSystemInfo[] filesAndDirectories_HeadDirectory)
        {
            // Check every file in the directory
            foreach (var item in filesAndDirectories_HeadDirectory)
            {
                if (fileNames_HeadDirectory.Contains(item.Name)) // If we work with file -> Add it as an entity
                    entities.Add(CreateEntityFromFile((FileInfo)item));
                else
                    ProceedDirectory(entities, (DirectoryInfo)item); // If we work with directory -> Start method for proceeding the directory

            }
        }

        // Method for calculating size of directory
        static long GetDirectorySize(DirectoryInfo dir)
        {
            // Set current size as 0
            long size = 0;

            // Get all the files and directories in the selected directory
            FileInfo[] files = dir.GetFiles();
            DirectoryInfo[] directories = dir.GetDirectories();

            // Add to the directory size variable, size of the current file
            foreach (var file in files)
                size += file.Length;

            // Add to the directory size variable, size of the current directory
            foreach (var directory in directories)
                size += GetDirectorySize(directory); // Before adding -> calculate the size using method

            // Return the result size
            return size;
        }

        // Method for creating Entity object from FileInfo object using standart constructor 
        static Entity CreateEntityFromFile(FileInfo file)
        {
            return new Entity
            {
                Info = file, // Save FileInfo for future size and persantage calculating
                Name = file.Name,
                Type = file.Extension == ".txt" ? EntityType.TextFile : EntityType.File,
                SubDirecory = file.Directory,
                //Size = file.Length,
                //Persantage = (100 * (float)file.Length / GetDirectorySize(file.Directory)).ToString() + "%"
            };
        }

        // Method for creating Entity object from DirectoryInfo object using standart constructor 
        static Entity CreateEntityFromDirectory(DirectoryInfo dir, bool isHeadDirectory = false)
        {
            return new Entity
            {
                Info = dir, // Save DirectoryInfo for future size and persantage calculating
                Name = dir.Name,
                Type = EntityType.Directory,
                SubDirecory = isHeadDirectory ? null : dir.Parent, // If we work with head directory -> subDirectory is null. Otherwise -> Parent
                //Size = GetDirectorySize(dir),
                //Persantage = isHeadDirectory ? String.Empty : (100 * (float)GetDirectorySize(dir) / GetDirectorySize(dir.Parent)).ToString() + "%"
            };
        }

        // Recursive method for proceeding the normal directories and inside directories aswell
        static void ProceedDirectory(List<Entity> entities, DirectoryInfo dir)
        {
            // Add to the list Entity? created from DirectoryInfo variable
            entities.Add(CreateEntityFromDirectory(dir));

            // Get files in the current folder
            var files_SubDirectory = dir.GetFiles();

            // Get all the files (normal files and directories) in the current directory
            var filesAndDirectories_SubDirectory = dir.GetFileSystemInfos();

            // Get all normal files name and save it into new list
            var fileNames_SubDirectory = new List<string>();
            foreach (var file in files_SubDirectory)
                fileNames_SubDirectory.Add(file.Name);

            // Check every file and directory in current directory
            foreach (var item in filesAndDirectories_SubDirectory)
            {
                // If we work with a normal file -> Save it in the main list
                if (fileNames_SubDirectory.Contains(item.Name))
                    entities.Add(CreateEntityFromFile((FileInfo)item));
                else
                    ProceedDirectory(entities, (DirectoryInfo)item); // If we work with directory -> start ProceedDirectory method
                                                                     // using the recursion with the new (current subDir) parameter

            }
        }

        // Methods for usage in a client side to change static variable wich will not allow application to create new threads for processing dir size
        public static void StopProcessing()
        {
            isWorking = false;
        }
    }
}