NAME = HeapShot.Gui.Widgets
TARGET = $(NAME).dll
CSPROJ = $(NAME).csproj
XBUILD = xbuild
XBUILD_ARGS = "/property:Configuration=Debug" $(CSPROJ)

CSFILES = \
	$(wildcard *.cs) \
	$(wildcard */*.cs)

RES =  \
	class.png \
	field.png \
	gtk-gui/gui.stetic \
	gtk-gui/objects.xml

$(TARGET): $(CSFILES) $(CSPROJ)
	$(XBUILD) $(XBUILD_ARGS)

all: $(TARGET)

install-data-local: $(TARGET)
	$(mkinstalldirs) $(DESTDIR)$(pkglibdir)
	$(INSTALL_DATA) $(TARGET) $(DESTDIR)$(pkglibdir)

uninstall-local:
	cd $(DESTDIR)$(pkglibdir) && rm -f $(TARGET)

EXTRA_DIST = 			\
	$(CSFILES) $(RES)

CLEANFILES =			\
	$(TARGET)		\
	$(TARGET).mdb

clean-local:
	$(XBUILD) $(XBUILD_ARGS) /t:Clean
	rm -rf bin obj
