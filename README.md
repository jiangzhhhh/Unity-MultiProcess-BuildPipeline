# Unity-MultiProcess-BuildPipeline
多进程资源构建方案

# 设计
- 通过符号链接(windows依赖junction)创建构建工程<br>
- 构建时对构建列表进行依赖分析，创建依赖树，并对依赖树进行结果分组，将有依赖关系的构建资源归纳为一组<br>
- 使用子进程方式启动多个Unity进程，进行资源构建


# 用例
1. 选择菜单"MultiProcessBuild/Profile"进入Profile面板,点击"Create Slaves"创建多个构建工程<br>
2. 使用"Exampl/Build"进行资源构建
