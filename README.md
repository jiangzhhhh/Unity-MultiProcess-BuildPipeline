# Unity-MultiProcess-BuildPipeline
多进程资源构建方案

# 设计
- 创建多组子工程进行并行构建
- 在Windows下，通过使用mklink命令创建子工程的Assets/ProjectSetting目录<br>
- 在OSX下，由于Unity不支持Assets目录为符号链接，因此为子工程创建真实Assets目录，然后将主工程Assets目录下的文件和文件夹符号链接到子工程的Assets目录下<br>
- 构建时对构建列表进行依赖分析，根据依赖关系进行任务分组，确保有依赖关系的构建资源从属同一节点<br>
- 使用子进程方式启动多个Unity进程，并行进行资源构建

# 数据对比
在ssd+i7的pc上进行实验，测试工程单节点进行全量资源构建需要300min，同样测试条件下，修改为5节点并行构建后缩短为42min，效果显著<br>

# 用例
使用"Exampl/Build"进行资源构建


# design
- Create multiple subprojects for parallel builds
- Under Windows, create the Assets/ProjectSetting directory of the salve build project by using 'mklink'
- Under OSX, since Unity does not support the Assets directory as a symbolic link on the OSX system, so, the real Assets directory is created for the subproject.Then link files and directories under the Assets directory of the master project to the Assets directory of the subproject.
- Dependency analysis of the build list before building.Group build assets, Make sure that the dependent assets belong to the same build group.

# single process VS multi process
My test project reduced the build time from 300mins to 42mins on the same PC(ssd+i7).

# usage
click menu "Exampl/Build"
